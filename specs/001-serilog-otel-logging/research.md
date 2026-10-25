# Research: Shared Structured Logging with Trace Correlation

## 1. Where the setup lives

**Decision**: Put all logging wiring in the existing `src/BuildingBlocks/BuildBlock.Logging` project. Keep the current folder/csproj name. Catalog (and later services) only reference the project, call `AddAppLogging()`, and own a `Serilog` section in `appsettings*.json`.

**Rationale**: The spec and the user request put configuration ownership in the logging building block. The project already exists (empty `Class1.cs`). Renaming to `BuildingBlock.Logging` would be churn for an unused scaffold and is not required to deliver the feature.

**Alternatives considered**:
- Put Serilog calls directly in Catalog `Program.cs` — rejected; FR-002 forbids duplicated wiring.
- Rename the project to `BuildingBlock.Logging` — rejected for this version; name mismatch is documented, not blocking.
- New OpenTelemetry building block — rejected; spec scopes OTEL to log correlation/export only.

## 2. Public enable API

**Decision**: One extension, matching the existing `AddAppCarter` style:

```csharp
public static WebApplicationBuilder AddAppLogging(this WebApplicationBuilder builder)
```

Implementation (Green phase) uses `builder.Host.UseSerilog((context, services, configuration) => …)` with `ReadFrom.Configuration` and `ReadFrom.Services`. Delete `Class1.cs`.

**Rationale**: `UseSerilog` replaces the host logger early enough for startup and request logs. `WebApplicationBuilder` is what Catalog already uses. One method, no interface.

**Alternatives considered**:
- `IServiceCollection.AddSerilog` only — later host lifetime logs are weaker; `UseSerilog` is the documented host pattern.
- Bootstrap `Log.Logger` in every `Program.cs` — extra per-service lines; SC-001 is met without it.
- Custom `ILoggingBuilder` wrapper — abstraction ban.

## 3. Per-service configuration

**Decision**: Each service owns a top-level `Serilog` section in `appsettings.json` and `appsettings.{Environment}.json`. The building block does not ship a shared settings file. Environment files override base files through normal ASP.NET configuration layering.

If the `Serilog` section is missing, the building block applies safe defaults in code: console output, `Information` minimum, `Microsoft.AspNetCore` overridden to `Warning`, no OTLP export, plus service name and environment enrichers.

**Rationale**: This is Serilog's native `Serilog.Settings.Configuration` model (`ReadFrom.Configuration`). It satisfies FR-003/FR-004/FR-005/FR-010 without a custom options type.

**Alternatives considered**:
- Bind a custom `LoggingOptions` POCO and map sinks in code — extra type, duplicates Serilog's schema.
- Keep the existing `Logging` MEL section as the source of truth — spec says it is superseded once the building block is enabled.
- Shared `appsettings` in the building block — fights per-service control.

## 4. OpenTelemetry integration (logs only)

**Decision**: Add `Serilog.Sinks.OpenTelemetry`. Services that want remote export include a `WriteTo` entry named `OpenTelemetry` with `endpoint` and `protocol` (`Grpc` or `HttpProtobuf`) and list `Serilog.Sinks.OpenTelemetry` in `Using`. Services that do not want export omit that entry.

Do **not** add OpenTelemetry.Extensions.Hosting, the tracing SDK, or a metrics pipeline.

**Rationale**: The sink converts Serilog events to OTLP logs. The spec explicitly leaves full traces/metrics out of scope unless needed for correlation IDs. Correlation does not need that SDK (see next decision). Batched OTLP export is fail-open: an unreachable collector must not crash request handling (FR-009).

**Alternatives considered**:
- OpenTelemetry .NET logging provider instead of Serilog — rejected; user required Serilog.
- Dual-write via `OpenTelemetry.Exporter.OpenTelemetryProtocol` + MEL — more packages, two pipelines.
- Always register the OTLP sink in code and read `OTEL_EXPORTER_OTLP_ENDPOINT` — weaker fit for "config in appsettings"; sink-from-appsettings is the documented pattern.

**Empty endpoint**: treat as omitted. Operators who do not want export leave the `WriteTo` entry out. Do not add a custom sink filter.

## 5. Trace / span correlation

**Decision**: Rely on Serilog 4+ first-class `TraceId` / `SpanId` on `LogEvent`, filled from `Activity.Current` when an activity listener exists. ASP.NET Core creates a request `Activity` by default. Console templates may include `{TraceId}` `{SpanId}`. The OTLP sink forwards those IDs.

No `Serilog.Enrichers.Span` package. No custom enricher for trace IDs.

**Rationale**: Serilog merged built-in activity capture (serilog#1955). An extra enricher would duplicate first-class fields. ASP.NET Core hosting already has an activity listener, which is the condition Serilog needs.

**Alternatives considered**:
- `Serilog.Enrichers.Span` — redundant on Serilog 4+.
- Full OTEL ASP.NET instrumentation — out of scope; only needed if request activities disappear (they do not on ASP.NET Core).
- Manual `LogContext.PushProperty` in middleware — extra code, easy to miss.

## 6. Service identity and environment

**Decision**: After `ReadFrom.Configuration`, always add properties `service.name` (application name) and `deployment.environment` (host environment name). Appsettings may also set `Serilog:Properties:Application`. Console output uses a compact structured template so identity is visible without opening source (SC-005).

**Rationale**: FR-006 requires service identity and environment on every record. Doing it in the building block means a service cannot forget them.

**Alternatives considered**:
- Only `Serilog:Properties` in each appsettings — services can omit them.
- `Serilog.Enrichers.Environment` `WithEnvironmentName` / `WithMachineName` only — useful extras; may be enabled from appsettings `Enrich` array. Building-block properties still required.

## 7. Secrets

**Decision**: Ship one sealed `SensitiveDataEnricher` in the building block that redacts scalar property names containing `Password`, `Secret`, `Token`, `ConnectionString`, or `Authorization` (case-insensitive) to `***`. Do not log `IConfiguration` objects. No general PII engine.

**Rationale**: FR-011 and the workspace safety net. A small enricher is the minimum that is testable. The plan authorizes this type; it is not a new layer.

**Alternatives considered**:
- Documentation only — not testable enough for FR-011.
- Full PII redaction library — out of scope (spec assumption).

## 8. Packages

**Decision**: Reference from `BuildBlock.Logging` only:

| Package | Role |
|---------|------|
| Serilog.AspNetCore | `UseSerilog`, MEL replacement, pulls Serilog + Settings.Configuration + Console |
| Serilog.Sinks.OpenTelemetry | Optional OTLP log export |
| Serilog.Enrichers.Environment | Available for appsettings `Enrich` names |

Use latest stable versions compatible with `net10.0` at implement time. Catalog gets logging via project reference, not extra package references.

**Alternatives considered**:
- Catalog references Serilog packages directly — duplicates versions; rejected.
- `Serilog.Sinks.Console` explicit reference — already brought in by Serilog.AspNetCore.

## 9. First consumer and solution

**Decision**: Catalog calls `builder.AddAppLogging()` at the start of `AddCatalogServices()`. Add a project reference and a `Serilog` section in both Catalog settings files. Add `BuildBlock.Logging` and `BuildBlock.Logging.Tests` to `eShop.slnx`.

**Rationale**: Catalog is the only service (FR-012). Keeping `Program.cs` as a two-step builder remains consistent with the current composition style.

## 10. Testing

**Decision**: New `tests/BuildingBlocks/BuildBlock.Logging.Tests` (xUnit). Tests boot a minimal `WebApplicationFactory` / generic host that calls `AddAppLogging()` with in-memory or temp JSON config and a capturing sink (Serilog `DelegatingSink` / `CollectingSink` pattern, or `Serilog.Sinks.InMemory` if a one-liner is cleaner).

Cover: enable + structured fields; missing `Serilog` section uses defaults; environment override of minimum level; OTLP entry omitted ⇒ no remote export and host still starts; request with `Activity` ⇒ TraceId/SpanId present; sensitive property names redacted.

TDD: write these tests first; production methods throw `NotImplementedException` until Green.

**Rationale**: No test projects exist today. Library contract tests are the constitution/integration gate. A full Catalog HTTP test is optional later; the test host is enough to prove the building block.

**Alternatives considered**:
- Only Catalog `WebApplicationFactory` tests — slower, couples logging proof to Catalog routes.
- No test project — violates TDD gate.
