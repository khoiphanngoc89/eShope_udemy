# Data Model: Shared Structured Logging

This feature does not persist entities. The model is the in-memory / on-the-wire shape of configuration and diagnostic records.

## Logging Configuration

Per-service settings bound from the top-level `Serilog` section (see [contracts/logging-configuration.schema.json](./contracts/logging-configuration.schema.json)).

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| Using | string[] | no | Assembly names Serilog must load (`Serilog.Sinks.Console`, `Serilog.Sinks.OpenTelemetry`, `Serilog.Enrichers.Environment`) |
| MinimumLevel.Default | severity | no | Default `Information` when omitted (building-block fallback) |
| MinimumLevel.Override | map of source → severity | no | At least `Microsoft.AspNetCore: Warning` in Catalog samples |
| Enrich | string[] | no | Named enrichers from packages (`FromLogContext`, `WithEnvironmentName`, `WithMachineName`) |
| Properties | map of string → string | no | Optional extra labels; does not replace building-block `service.name` / `deployment.environment` |
| WriteTo | sink list | no | Each item has `Name` and optional `Args` |
| WriteTo[].Name = Console | sink | recommended | Local output; implied by defaults when section is missing |
| WriteTo[].Name = OpenTelemetry | sink | no | Omit entirely when remote export is off |
| WriteTo OpenTelemetry.Args.endpoint | URI | yes if sink present | OTLP collector base URL |
| WriteTo OpenTelemetry.Args.protocol | `Grpc` \| `HttpProtobuf` | no | Default `Grpc` |

### Validation

- Missing entire `Serilog` section: valid. Apply safe defaults. Do not fail startup.
- Present section with no `WriteTo`: valid. Building block still adds identity enrichers; if Serilog then has no sink, building-block defaults MUST still write to console (FR-010).
- OpenTelemetry sink with missing endpoint: treat as operator error in that service's file; prefer omitting the sink. Startup must still succeed if the sink package fails open (FR-009).
- Environment-specific file: same schema; last-in ASP.NET config wins.

### Relationships

- One **Logging Configuration** per service process.
- Configuration produces many **Diagnostic Records**.
- Optional OpenTelemetry sink forwards records to **Observability Export**.

## Diagnostic Record

One structured event emitted by Serilog.

| Field | Type | Required | Source |
|-------|------|----------|--------|
| Timestamp | datetime | yes | Serilog |
| Severity | Verbose…Fatal | yes | Serilog level |
| Message | string | yes | template + properties |
| service.name | string | yes | Building block (application name) |
| deployment.environment | string | yes | Building block (environment name) |
| TraceId | string | no | `Activity.Current` when a request activity exists |
| SpanId | string | no | `Activity.Current` when a request activity exists |
| Additional properties | map | no | Caller / enrichers |

### Rules

- Records written during a request with `Activity.Current` MUST include TraceId and SpanId matching that activity (FR-007, SC-003).
- Records written with no activity MUST remain valid and omit those IDs (US3-4).
- Property names matching sensitive tokens MUST be stored as `***` (FR-011).

## Service Identity

| Field | Type | Required | Notes |
|-------|------|----------|--------|
| service.name | string | yes | `IHostEnvironment.ApplicationName` (e.g. `eShop.Catalog.Api`) |
| deployment.environment | string | yes | `Development`, `Production`, … |

Used to distinguish two services on the same machine (edge case, SC-005, SC-006).

## Trace Correlation

| Field | Type | When present |
|-------|------|----------------|
| TraceId | 32-hex W3C | Request (or any) activity is current |
| SpanId | 16-hex W3C | Same |

Not a stored entity. Copied onto the diagnostic record. No parent-span field required for this version.

## Observability Export

| Field | Type | Required | Notes |
|-------|------|----------|--------|
| Enabled | bool | derived | True only when an OpenTelemetry `WriteTo` entry exists |
| Endpoint | URI | when enabled | From appsettings |
| Protocol | enum | when enabled | `Grpc` or `HttpProtobuf` |

State: **Off** (no sink) → **On** (sink configured). There is no retry state machine in this feature; the sink batches and fails open.
