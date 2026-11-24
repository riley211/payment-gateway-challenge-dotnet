# Payment Gateway

An API that lets a merchant take card payments through an acquiring bank and retrieve them later. A payment is **Authorized** or **Declined** by the bank, or **Rejected** by the gateway before the bank is called if the request is invalid.

Built with .NET 10 and ASP.NET Core. The acquiring bank is a Mountebank simulator.

## Quick start

Requires Docker.

```
docker compose up --build
```

| What | URL |
|---|---|
| Payment gateway API | http://localhost:8090 |
| Swagger UI, to try the API in a browser | [http://localhost:8090/swagger](http://localhost:8090/swagger) |
| Aspire Dashboard: traces, metrics and logs | [http://localhost:18888](http://localhost:18888) |
| Bank simulator | http://localhost:8080 (admin on [http://localhost:2525](http://localhost:2525)) |

In Swagger, open `POST /api/payments`, choose **Try it out**, then **Execute**. The example request is valid and returns `201 Authorized`. [`PaymentGateway.Api.http`](src/PaymentGateway.Api/PaymentGateway.Api.http) has a request for every outcome, for VS Code (REST Client), Visual Studio or Rider.

### Test cards

The bank simulator decides the outcome from the **last digit** of the card number:

| Card number | Outcome |
|---|---|
| `2222405343248877`, or any number ending 1, 3, 5, 7 or 9 | `201` Authorized |
| `2222405343248878`, or any number ending 2, 4, 6 or 8 | `201` Declined |
| `2222405343248870`, or any number ending 0 | `502`, the bank is unavailable |

### Running without Docker

```
docker compose up bank_simulator
dotnet run --project src/PaymentGateway.Api
```

The gateway then runs on https://localhost:7092 ([Swagger](https://localhost:7092/swagger)). Telemetry export is off unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set.

## API

### `POST /api/payments`: process a payment

```json
{
  "cardNumber": "2222405343248877",
  "expiryMonth": 4,
  "expiryYear": 2030,
  "currency": "GBP",
  "amount": 1050,
  "cvv": "123"
}
```

| Field | Rules |
|---|---|
| `cardNumber` | Required, 14–19 digits |
| `expiryMonth` | Required, 1–12 |
| `expiryYear` | Required. Month and year together must not be in the past; a card is valid until the end of its expiry month. |
| `currency` | Required, one of `GBP`, `USD`, `EUR` |
| `amount` | Required, a whole number of at least 1, in the minor currency unit (`1050` = 10.50) |
| `cvv` | Required, 3–4 digits |

The optional **`Idempotency-Key`** header makes retries safe. Repeating a request with the same key returns the original payment without charging the card again, even if the requests arrive at the same moment.

| Status | When | Body |
|---|---|---|
| `201 Created` | The bank authorised or declined the payment | The payment (below), with a `Location` header for `GET` |
| `400 Bad Request` | The request is invalid. The payment is **Rejected**, and the bank isn't called. | ProblemDetails with `paymentStatus: "Rejected"` and a message per invalid field |
| `422 Unprocessable Entity` | The `Idempotency-Key` was already used for a different payment | ProblemDetails |
| `502 Bad Gateway` | The bank is unavailable, times out or responds unexpectedly. Nothing is stored. | ProblemDetails |

```json
{
  "id": "cde87ead-c4d0-4a48-8bb0-c65526dbc337",
  "status": "Authorized",
  "cardNumberLastFour": "8877",
  "expiryMonth": 4,
  "expiryYear": 2030,
  "currency": "GBP",
  "amount": 1050
}
```

A rejected request:

```json
{
  "title": "The payment was rejected because some fields are invalid.",
  "status": 400,
  "errors": { "currency": ["Currency must be one of GBP, USD or EUR."] },
  "paymentStatus": "Rejected"
}
```

### `GET /api/payments/{id}`: retrieve a payment

Returns `200` with the same body as above, or `404` if there is no payment with that id. Only the last four digits of the card number are ever returned.

### `GET /health`

Returns `200 Healthy` while the service can handle requests.

## Tests

```
dotnet test
```

| Project | What it covers | Needs |
|---|---|---|
| `test/PaymentGateway.Api.Tests` | **Unit tests:** every validation rule at its boundaries, the bank client against a stubbed HTTP handler, concurrent writes to the repository. **API tests:** the real ASP.NET pipeline in memory with a fake bank, covering every outcome, idempotency (including concurrent duplicates), logging with no card data, and metrics. | Nothing |
| `test/PaymentGateway.Api.IntegrationTests` | The gateway against the real Mountebank simulator, started by Testcontainers with the repository's `imposters/` | Docker running |

To run only the fast suite: `dotnet test test/PaymentGateway.Api.Tests`

## Project structure

```
src/PaymentGateway.Api/
  Controllers/   PaymentsController, RejectedPaymentResponse
  Models/        Requests, Responses, Bank (the simulator's request and response)
  Services/      PaymentsService, BankSimulatorClient (IAcquiringBank),
                 IdempotencyStore, PaymentsRepository, PaymentMetrics
test/
  PaymentGateway.Api.Tests/              unit and in-memory API tests
  PaymentGateway.Api.IntegrationTests/   tests against the real bank simulator
docs/design/     design decisions, assumptions and the requirements checklist
imposters/       the bank simulator's configuration (unchanged)
```

The template's structure and signatures are kept wherever possible, so changes are easy to compare with the original.

## Design

The key decisions and their reasons are in [`docs/design`](docs/design):

- **[01 – Requirements, decisions and assumptions](docs/design/01-requirements-and-assumptions.md):** the requirements checklist with the tests that prove each item, design decisions, assumptions, and the queue-based production architecture.
- **[02 – Idempotency and failure handling](docs/design/02-idempotency-and-failure-handling.md):** how double charges are prevented, and the unknown-outcome problem after a bank timeout.
- **[03 – Observability and hosting](docs/design/03-observability-and-hosting.md):** logs, metrics, traces, health, and the Docker setup.

In short:

- **Synchronous contract, asynchronous implementation.** `POST` returns the final status, as the brief requires, and is `async` end to end.
- **No automatic retries to the bank.** It takes no idempotency key, so a retry could authorise twice. Merchants retry safely with `Idempotency-Key` instead.
- **Card data:** the full card number and CVV are only sent to the bank. They are never stored, returned or logged.
- **Storage** is the template's in-memory repository, made safe for concurrent requests.
- **Observability** uses vendor-neutral OpenTelemetry, viewed locally in the Aspire Dashboard.

## Production next steps

| Area | Next step |
|---|---|
| Processing model | Accept with `202`, process through an outbox, a queue and workers, and notify by webhook (see document 01) |
| Unknown outcomes | Send the bank an idempotency key, record each attempt before calling it, and reconcile timeouts against the bank's records |
| Storage | A database, with a domain model separate from the API contract |
| Idempotency | Keys in the shared database or Redis, scoped per merchant, with a time to live and an HMAC fingerprint |
| Security | Merchant authentication (API keys or OAuth), and card data tokenised by a PCI-scoped vault |
| Decline reasons | Store and return the acquirer's decline code, so merchants know whether a retry can succeed |
| Card validation | A Luhn checksum to catch mistyped card numbers before calling the bank |
| Observability | Export OTLP to a Collector feeding the team's monitoring platform, with alerts on bank failure rate, bank latency and rejection spikes |
| Delivery | CI building and testing one image per commit, image scanning, and deployment to Kubernetes or ECS behind TLS, with liveness and readiness probes |
