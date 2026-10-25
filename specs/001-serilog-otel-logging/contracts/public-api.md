# Contract: BuildBlock.Logging public API

Consumers are ASP.NET Core services (Catalog first). This is the only public surface the building block adds.

## `AddAppLogging`

```text
WebApplicationBuilder AddAppLogging(this WebApplicationBuilder builder)
```

### Preconditions

- `builder` is not null.
- The host has not been built yet.

### Behavior

1. Registers Serilog as the host logging provider (`UseSerilog`).
2. Reads the `Serilog` configuration section from `builder.Configuration` when it exists.
3. If that section is missing, applies safe defaults: console sink, `Information` minimum, `Microsoft.AspNetCore` → `Warning`, no OpenTelemetry sink.
4. Always enriches events with `service.name` and `deployment.environment`.
5. Always redacts sensitive scalar property names (`Password`, `Secret`, `Token`, `ConnectionString`, `Authorization`).
6. Returns the same `WebApplicationBuilder` for chaining.

### Service usage

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddAppLogging();
```

The service MUST supply per-environment `Serilog` settings in its own `appsettings*.json` (see [logging-configuration.schema.json](./logging-configuration.schema.json)). The service MUST NOT add its own Serilog package references or call `UseSerilog` again.

### Errors

| Condition | Result |
|-----------|--------|
| `builder` is null | Throw `ArgumentNullException` |
| Optional OpenTelemetry endpoint missing or unreachable | Host still starts; local diagnostics continue |
| Invalid Serilog JSON that the settings package cannot parse | Startup fails with a configuration error that names the setting |

### Out of scope

- Tracing or metrics SDK registration
- File / Seq / cloud-monitor sinks
- A second public type besides `LoggingExtensions` and the internal redaction enricher
