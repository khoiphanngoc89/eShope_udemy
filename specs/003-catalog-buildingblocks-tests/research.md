# Research: Catalog and BuildingBlocks Automated Tests

## 1. Isolated catalog store vs live Postgres / Testcontainers

**Decision**: Use an in-suite `IDocumentSession` substitute (NSubstitute plus a dictionary of stored `Product`s). Do not run PostgreSQL or Testcontainers for this feature.

**Rationale**: Catalog already uses Marten `IDocumentSession` (`Store` + `SaveChangesAsync`). The spec requires an isolated store, sub-2-minute isolated checks, and no Grafana. A live database would add Docker, slow SC-003, and a new operational dependency. Capturing `Store` and answering `LoadAsync` proves the product was stored (FR-001, FR-002) without a real engine.

**Alternatives considered**:
- Testcontainers PostgreSQL + real Marten — true engine, slower, needs Docker; rejected for SC-002/SC-003 and Ponytail.
- Marten “in memory” — not a supported production substitute for this stack.
- Extract a repository interface in production — abstraction ban; not in the spec.

## 2. Service-level host

**Decision**: `WebApplicationFactory<Program>` in `eShop.Catalog.Api.Tests`. Happy path sets `ConnectionStrings:CatalogDb` to a non-empty dummy string (passes `DatabaseOptions` ValidateOnStart) and replaces `IDocumentSession` in `ConfigureTestServices` with the same in-suite substitute. Tests call `POST /products` and then load the product by the returned id from the substitute.

**Rationale**: FR-002 requires the public create path. There is no GET product API (FR-012 forbids adding one). Loading through the substitute is how “retrievable from the store” is proven. Dummy connection string never opens a socket if the session is replaced before first use.

**Alternatives considered**:
- `dotnet run` + curl — not an automated suite.
- Start real Marten against dummy connection — would fail on first save.
- Add GET `/products/{id}` only for tests — out of scope (FR-012).

## 3. Production seams required for tests

**Decision**: Two production edits only:

1. `public partial class Program { }` at the bottom of `src/Services/eShop.Catalog.Api/Program.cs` so `WebApplicationFactory<Program>` can compile.
2. `<InternalsVisibleTo Include="eShop.Catalog.Api.Tests" />` on `eShop.Catalog.Api.csproj` so isolated tests can construct internal `CreateProductHandler`.

**Rationale**: Handler is `internal`. Top-level Program is implicit and inaccessible. These are testability seams, not behavior changes. FR-009 still holds (no logging/Loki/export changes).

**Alternatives considered**:
- Make handler public — wider API than needed.
- Test only via HTTP — would skip isolated handler coverage (FR-001).
- Reflection to call internal handler — brittle.

## 4. BuildingBlock isolated checks

**Decision**: New `tests/BuildingBlocks/BuildingBlock.Tests` (do not extend `BuildBlock.Logging.Tests`).

- Command handling: test-assembly `ICommand<T>` + `ICommandHandler<T,T>` registered with MediatR; `ISender.Send` returns the handler result (FR-003).
- Request entry points: `AddAppCarter` on a `WebApplicationBuilder` plus a test `ICarterModule` in that assembly; host maps Carter and the module’s route responds (FR-004). Catalog `POST /products` remaining in Catalog tests proves Catalog’s own modules register.

**Rationale**: Logging tests are a different building block (FR-008). `BuildingBlock` has no production logic beyond interfaces and `AddAppCarter`. A sample command in the test project is the smallest proof the shared command types work with MediatR. `AddAppCarter` uses `Assembly.GetEntryAssembly()` (testhost during tests); a module in the test assembly still proves registration without Catalog.

**Alternatives considered**:
- Only test Catalog HTTP and skip BuildingBlock — fails US2 / FR-003 / FR-004.
- Put BuildingBlock tests inside Catalog.Tests — couples later services to Catalog create-product (US2 scenario 4).

## 5. Missing catalog store setting (FR-007)

**Decision**: Service-level test builds the Catalog host **without** `ConnectionStrings:CatalogDb` (empty or omitted). Starting the host / creating the client MUST throw an options validation error whose message contains `ConnectionStrings:CatalogDb`.

**Rationale**: Production already uses `ValidateOnStart` with that message in `DependencyInjection.cs`. The test records current behavior; it does not add new validation.

**Alternatives considered**:
- Change the error text — out of scope.
- Skip this check — fails FR-007 / US3.

## 6. Package and solution layout

**Decision**: Copy package versions from `tests/BuildingBlocks/BuildBlock.Logging.Tests`. Add NSubstitute only to Catalog tests. Add both projects to `eShop.slnx` under `/tests/Services/` and `/tests/BuildingBlocks/`.

**Rationale**: One version set. Solution already groups logging tests under `/tests/BuildingBlocks/`.

**Alternatives considered**:
- New test SDK versions — drift.
- One mega test project — mixes Catalog and BuildingBlock (rejected by US2 independence).

## 7. What not to touch

**Decision**: Do not edit `BuildBlock.Logging`, logging tests, Catalog `Serilog` / OTLP settings, or `deploy/observability/compose.yaml`. Do not add product read/update/delete. Do not add create-product field validation unless a test of *current* behavior requires documenting a throw that already exists (today the handler accepts empty name / negative price).

**Rationale**: FR-008, FR-009, FR-012, spec edge cases.

**Alternatives considered**:
- Reject empty name in production to make tests “stricter” — extra behavior not requested.
