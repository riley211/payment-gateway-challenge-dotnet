# 01 – Requirements, design decisions and assumptions

This is the baseline for the payment gateway. It records what we are building, the decisions that shape the design, and the assumptions where the brief leaves room for interpretation. Later documents in this folder go deeper on individual topics.

## Scope

A merchant-facing API that can:

1. **Process a payment.** It validates the request, forwards it to the acquiring bank (a Mountebank simulator) and returns the outcome.
2. **Retrieve a payment.** It returns a previously processed payment by its id, for reconciliation and reporting.

The gateway is the only component we build. The shopper, the merchant's checkout and the acquiring bank are outside it.

## Requirements checklist

Items are ticked once they are implemented and covered by a test.

### Processing a payment
- [ ] A merchant can process a payment and receive one of:
  - **Authorized**: the acquiring bank authorised the payment
  - **Declined**: the acquiring bank declined the payment
  - **Rejected**: the request was invalid, no payment was created, and the bank was not called
- [ ] Card number: required, 14–19 characters, numeric only
- [ ] Expiry month: required, 1–12
- [ ] Expiry year: required, in the future
- [ ] Expiry month and year together are in the future
- [ ] Currency: required, 3 characters, ISO 4217, validated against no more than 3 currency codes
- [ ] Amount: required, integer, in the minor currency unit (0.01 USD = `1`, 10.50 USD = `1050`)
- [ ] CVV: required, 3–4 characters, numeric only
- [ ] The response includes: id, status (Authorized / Declined), the last four card digits (never the full number), expiry month, expiry year, currency, amount

### Retrieving a payment
- [ ] A merchant can retrieve a previous payment by its id
- [ ] The response includes the same fields: id, status, last four card digits, expiry month, expiry year, currency, amount

### Acquiring bank integration
- [ ] `POST /payments` on the simulator with `card_number`, `expiry_date` (`MM/YYYY`), `currency`, `amount`, `cvv`
- [ ] The response fields `authorized` and `authorization_code` are mapped
- [ ] The simulator's behaviour is handled: odd last digit → authorised, even → declined, `0` → `503`, missing field → `400`

### Storage
- [ ] No real database. The in-memory repository from the template stands in for storage.

### Documentation
- [ ] Key design considerations and assumptions are documented (this folder, plus the root README)

### Implementation considerations
- [ ] Compiles
- [ ] Covered by automated tests
- [ ] Simple and maintainable, not over-engineered
- [ ] API design and architecture focus on the functional requirements

### Additional non-functional requirements
These aren't in the brief. They are the properties a payment system needs, so we hold ourselves to them.
- [ ] Asynchronous: non-blocking I/O, async end to end
- [ ] Idempotent: a retried or duplicated request never charges the shopper twice
- [ ] Production evolution to a queue-based design is documented
- [ ] Runs in Docker (gateway + bank simulator via docker compose)
- [ ] Observable: structured logs, health check, traces and metrics

## Design as built

```mermaid
flowchart LR
    Merchant -->|POST / GET| Gateway[Payment Gateway]
    Gateway -->|POST /payments| Bank[Bank simulator]
    Gateway --> Store[(In-memory store)]
```

What happens to a `POST /api/payments`:

| Situation | Bank called? | Stored? | Response |
|---|---|---|---|
| Request is invalid | No | No | `400` Rejected |
| Same `Idempotency-Key` and body as an earlier request | No | — | The original response |
| Bank authorises | Yes | Yes | `201` Authorized |
| Bank declines | Yes | Yes | `201` Declined |
| Bank unavailable (503 or timeout) | Yes | No | `502` |

`GET /api/payments/{id}` returns `200` with the payment, or `404` if the id is unknown.

## Key design decisions

### 1. Synchronous contract, asynchronous implementation
The brief says the payment response carries the outcome, Authorized or Declined. So `POST` waits for the bank and returns the final status. Everything on that path is `async`/`await`, so no thread is blocked while the bank responds and the service still scales under load.

A fully queue-based design was considered: accept with `202`, process in the background, and have the merchant poll. It was rejected for this exercise. It would change the contract the brief defines, add a `Pending` status the brief doesn't have, and add moving parts the requirements don't need. It is the right shape for production, so it is described under [Production evolution](#production-evolution).

### 2. Idempotency: never charge the shopper twice
Merchants retry. Networks time out, users double-click, and a merchant can't tell "the request was lost" from "the response was lost". Without protection, a retry is a second charge.

The merchant may send an `Idempotency-Key` header:
- The first request with a key is processed normally.
- A repeat with the same key and the same body returns the original result **without calling the bank again**. This also holds if the repeat arrives while the first request is still in flight.
- A repeat with the same key but a different body returns `422`. The key has already been used for a different payment.

Details are in [02-idempotency-and-failure-handling.md](02-idempotency-and-failure-handling.md) (added with the implementation).

### 3. No automatic retries to the bank
Retrying a failed call is a common resilience pattern, but it is unsafe here. The simulator's API takes no idempotency key, so the gateway can't tell the bank "this is the same payment as before". A retry after a timeout could create a second authorisation. The gateway fails fast and leaves the retry decision to the merchant, whose idempotency key makes that retry safe on our side.

### 4. Bank failures return 502 and store nothing
If the bank returns `503`, times out or responds unexpectedly, the gateway returns `502 Bad Gateway` with a ProblemDetails body. `502` says the problem is upstream of a healthy gateway. No payment is stored, because there is no outcome to record, and the idempotency key is released so the merchant can retry.

### 5. Card data handling
- The full card number and CVV are used only to call the bank. They are **never stored, returned or logged**.
- The stored payment and every response hold only the last four digits.
- Card number, CVV and last four are **strings**, not integers. They are identifiers, not quantities. As integers, leading zeros would be lost (`0123` would become `123`) and a 19-digit card number would overflow.

### 6. Validation uses the framework
Request rules are declared with built-in DataAnnotations on the request model. The one cross-field rule (expiry month and year in the future) is implemented with `IValidatableObject`. `[ApiController]` turns failures into a `400` before the controller runs, which is what guarantees a Rejected request never reaches the bank. No validation library is added, because the rules are simple enough that the framework covers them.

### 7. Storage
Storage is the in-memory repository from the template, made thread-safe. It sits behind an interface so a real database could replace it without touching the rest of the code.

### 8. Runtime
The template targets .NET 8, and we keep it to stay aligned with the exercise. .NET 8 reached end of support on 10 November 2026. Moving to .NET 10 (LTS) would be the first step before any production use.

## Assumptions

| Area | Assumption |
|---|---|
| Rejected response | `400 Bad Request` with a `ValidationProblemDetails` body listing each failed field, plus `"status": "Rejected"`. Nothing is stored, so a rejected payment has no id and can't be retrieved. |
| Currencies | `GBP`, `USD` and `EUR`, upper-case only. The brief asks for no more than three codes. |
| Amount | Must be greater than zero. A zero or negative payment has no meaning. |
| Expiry | A card is valid until the end of its expiry month, so the current month is accepted. "Now" comes from an injected `TimeProvider` so tests can control it. |
| Expiry year | A four-digit year, for example `2027`. |
| Bank failure | `503`, a timeout or an unexpected response all map to `502`. No payment is stored. |
| Idempotency-Key | Optional. Without it a request is always processed as new. Keys are held in memory for the lifetime of the process. |
| Retrieving | An unknown id returns `404`. |
| Authentication | Out of scope. All callers are treated as one merchant. |

## Out of scope, and what production would need

| Not built | Production approach |
|---|---|
| Merchant authentication | API keys or OAuth client credentials. Payments and idempotency keys are scoped per merchant. |
| Persistent storage | A database. The payment and its idempotency record are written in one transaction. |
| PCI-compliant card handling | Card data is tokenised by a PCI-scoped vault. The gateway handles tokens, not card numbers. |
| Multi-instance idempotency | Keys are stored in the shared database or a distributed cache with expiry, not in process memory. |
| Unknown bank outcomes | Reconciliation against the bank's records when a call times out after the bank may have acted. See document 02. |

## Production evolution

At production scale the bank call moves off the request path. The merchant gets an immediate, durable acknowledgement, and the outcome follows by webhook or polling:

```mermaid
flowchart LR
    Merchant -->|POST| API[Gateway API]
    API -->|save as Pending| DB[(Database)]
    DB --> Queue[[Queue]]
    Queue --> Worker
    Worker --> Bank
    Worker -->|Authorized / Declined| DB
```

1. The API saves the payment as `Pending` and returns `202 Accepted` straight away.
2. A worker takes it from the queue, calls the bank and records the outcome. The merchant then gets the outcome by webhook or by polling `GET`.

This needs three supporting pieces:
- **An outbox:** the payment and its queue message are written in one database transaction, so a payment can't be saved without also being queued.
- **Bank-side idempotency keys**, so the worker can retry safely.
- **A reconciliation job** for payments whose outcome is unknown after a timeout.
