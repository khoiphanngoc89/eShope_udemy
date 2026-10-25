# Quickstart: Shared Structured Logging

Validation guide for `001-serilog-otel-logging`. Implementation steps belong in `tasks.md` (not created by `/speckit-plan`).

## Prerequisites

- .NET 10 SDK
- Repo builds (`eShop.slnx`)
- Spec and contracts:
  - [spec.md](./spec.md)
  - [contracts/public-api.md](./contracts/public-api.md)
  - [contracts/logging-configuration.schema.json](./contracts/logging-configuration.schema.json)
  - [data-model.md](./data-model.md)

## Enable (Catalog)

1. Reference `BuildBlock.Logging` from `eShop.Catalog.Api`.
2. Call `builder.AddAppLogging()` before the host is built (start of `AddCatalogServices()`).
3. Replace the MEL `Logging` section with a `Serilog` section in:
   - `src/Services/eShop.Catalog.Api/appsettings.json`
   - `src/Services/eShop.Catalog.Api/appsettings.Development.json`

Minimum Catalog settings (local only — no remote export):

```json
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Enrichers.Environment" ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": { "Microsoft.AspNetCore": "Warning" }
    },
    "Enrich": [ "FromLogContext", "WithEnvironmentName" ],
    "WriteTo": [ { "Name": "Console" } ]
  }
}
```

Catalog's base settings export every eligible record to `http://localhost:4318` (`HttpProtobuf`) for a Podman-hosted Grafana LGTM stack. Leave `endpoint` empty to disable export.

Start Grafana (default ports) from the repo root:

```bash
podman compose -f deploy/observability/compose.yaml up -d
```

Grafana UI: `http://localhost:3000` (admin / admin). Then run Catalog and open Explore → Loki (or the LGTM logs view) to see `eShop.Catalog.Api` records.

## Prove it with tests (required)

From repo root, after the test project exists:

```bash
dotnet test tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj
```

Expected: all tests green.

| Scenario | Expected |
|----------|----------|
| Enable with valid settings | Structured events include timestamp, severity, message, `service.name`, `deployment.environment` |
| Missing `Serilog` section | Host starts; console defaults; no OTLP sink |
| Environment override of `MinimumLevel` | Only the overridden service/environment changes verbosity |
| Request with `Activity.Current` | ≥95% of events in that request have matching TraceId/SpanId |
| No activity | Events still valid; TraceId/SpanId absent |
| Property named `Password` / `ConnectionString` | Value stored as `***` |
| OpenTelemetry sink omitted or collector down | Host starts; local events still written |

TDD order: those tests fail first (`NotImplementedException` in `AddAppLogging`). Then implement the minimum to turn them green.

## Prove it by running Catalog

```bash
dotnet run --project src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj
```

Expected within one minute of a successful start:

- Console shows structured startup diagnostics for `eShop.Catalog.Api` and the current environment.
- Hitting an existing Catalog route (e.g. `POST /products`) writes request-time events with the same shape.
- If a request activity is present, those events include TraceId/SpanId.

OTLP check: with the Podman Grafana stack up (`http://localhost:4318`), start Catalog and confirm the same records appear in Grafana within 2 minutes. If the stack is down, Catalog still starts and writes local diagnostics.

## Out of scope for this validation

- File, Seq, or cloud-monitor sinks
- Metrics or a full tracing product
- A second eShop service (pattern is the same call + its own `appsettings`)
