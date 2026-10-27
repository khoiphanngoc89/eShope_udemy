# Contract: BuildingBlock public helpers

Consumers are ASP.NET Core services (Catalog first). Isolated checks live in `tests/BuildingBlocks/BuildingBlock.Tests`. Do not use `BuildBlock.Logging.Tests` for these cases (FR-008).

## 1. Shared command handling (FR-003)

```text
ICommand<TResponse> : IRequest<TResponse>
ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
```

**Precondition**: Test host registers MediatR and one handler in the test assembly.

**Success**

- `ISender.Send(sampleCommand)` completes
- Result equals the handler’s return value

**Failure of the feature**

- Send throws because the command type is not a MediatR request
- No handler is found for the sample command

## 2. Request-entry registration (FR-004)

```text
IServiceCollection AddAppCarter(this IServiceCollection services)
```

**Precondition**: `services` is from a `WebApplicationBuilder` that has not been built. A test `ICarterModule` exists in an assembly `AddAppCarter` will scan (test entry assembly is acceptable).

**Success**

- `AddAppCarter` returns the same `IServiceCollection`
- After `MapCarter()`, the test module’s route responds (non-404)

**Errors**

| Condition | Result |
|-----------|--------|
| Entry assembly cannot be determined | `InvalidOperationException` with message that the entry assembly could not be determined |

Catalog’s own `POST /products` (see [create-product-api.md](./create-product-api.md)) additionally proves Catalog modules register when the Catalog host starts.

## Out of scope

- Serilog / `AddAppLogging`
- Grafana, Loki, OTLP
- Changing `AddAppCarter` to take an explicit assembly list unless a failing test requires it
