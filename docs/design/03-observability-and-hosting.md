# 03 – Observability and hosting

How the gateway is packaged and run, and how you can see what it's doing. Everything runs locally with no vendor platform, but nothing is tied to running locally.

## Running it

```
docker compose up --build
```

| Service | URL | Purpose |
|---|---|---|
| Payment gateway | http://localhost:8090 (Swagger at `/swagger`) | The API |
| Aspire Dashboard | http://localhost:18888 | Traces, metrics and logs from the gateway |
| Bank simulator | http://localhost:8080, admin on `:2525` | The template's Mountebank simulator, unchanged |

## Packaging

- **Multi-stage `Dockerfile`.** The .NET 10 SDK image restores and publishes, and the smaller `aspnet:10.0` runtime image runs the result. The project file is copied and restored before the source, so the restore layer is cached between builds.
- **Runs as a non-root user** (`$APP_UID`, provided by the base image).
- **Configuration comes from environment variables,** not code. For example, `BankSimulator__BaseUrl` points the gateway at `bank_simulator` on the compose network. The same image runs anywhere by changing environment variables only.
- **`ASPNETCORE_ENVIRONMENT=Development` in compose** so Swagger is available for the demo. A production deployment would run as `Production`.
- **HTTP only inside the container.** In production, TLS terminates at the load balancer or ingress in front of the service. The template's HTTPS redirection is kept for local `dotnet run`. In the container it has no HTTPS port, so it logs one warning on the first request and does nothing.
- **No compose `healthcheck`.** The runtime image deliberately contains no `curl` or `wget`. Orchestrators such as Kubernetes probe `/health` over HTTP themselves, so no tool is needed inside the image.

## Observability

| Signal | What the gateway produces | Where to see it locally |
|---|---|---|
| **Logs** | Structured JSON in the container (`Logging__Console__FormatterName=json`). One line per outcome: Authorized/Declined with payment id, amount and currency; Rejected with the invalid field *names*; warnings for bank failures and reused idempotency keys. | `docker compose logs payment_gateway`, or the dashboard's Structured logs page |
| **Metrics** | `payment_gateway.payments` on the `PaymentGateway` meter, tagged `outcome` = Authorized / Declined / Rejected / BankUnavailable. Also the built-in `http.server.request.duration` and `http.client.request.duration`, which give bank latency. | Dashboard → Metrics |
| **Traces** | One trace per request. If the bank was called, it contains a child span for the bank call with its timing. Health checks are filtered out. | Dashboard → Traces |
| **Health** | `GET /health` returns `Healthy` while the process can serve requests. | `curl http://localhost:8090/health` |
| **Errors** | Every error response is ProblemDetails, including unexpected 500s, and carries a `traceId` that matches the trace and the log lines. | Response body |

**Card data never reaches telemetry.** The full card number and CVV are never logged, and a test sends Authorized, failed and Rejected payments and checks that neither value appears in any log record.

**Export is vendor-neutral.** Telemetry goes out over OpenTelemetry's OTLP protocol, and only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set (compose sets it to the dashboard). Tests and `dotnet run` export nothing. The Aspire Dashboard is a local development tool: it's unauthenticated and keeps data in memory.

## What production would add

| Area | Production approach |
|---|---|
| Telemetry backend | Point OTLP at an OpenTelemetry Collector, which forwards to Datadog, Azure Monitor / App Insights, Grafana and so on. No code changes. |
| Alerts | Bank-unavailable rate (`outcome=BankUnavailable`), p99 bank latency, 5xx rate, a spike in Rejected for one merchant (likely an integration bug) |
| Health | Separate liveness (`/health`, the process is up) and readiness probes (dependencies such as the database are reachable). The bank shouldn't fail readiness, or an acquirer outage would take the whole gateway out of rotation. |
| Hosting | Push the image to a registry and run several replicas on Kubernetes or ECS behind a TLS-terminating load balancer. Secrets come from a secrets manager. |
| Multiple replicas | Needs shared storage and a shared idempotency store (see documents 01 and 02) |
| Runtime image | A smaller, distroless "chiseled" .NET image, and image scanning in CI |
