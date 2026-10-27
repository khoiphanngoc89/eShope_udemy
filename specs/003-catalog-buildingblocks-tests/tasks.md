# Tasks: Catalog and BuildingBlocks Automated Tests

**Input**: Design documents from `/specs/003-catalog-buildingblocks-tests/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included. The feature *is* automated checks. Workspace TDD: write tests first; they must compile against the Phase 2 seams. Existing `CreateProductHandler` should make US1 green without new product validation. Change production only if a test fails for a real bug. Do not edit `BuildBlock.Logging` or `deploy/observability/`.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- Catalog tests: `tests/Services/eShop.Catalog.Api.Tests/`
- BuildingBlock tests: `tests/BuildingBlocks/BuildingBlock.Tests/`
- Logging tests (do not modify): `tests/BuildingBlocks/BuildBlock.Logging.Tests/`
- Production seams only: `src/Services/eShop.Catalog.Api/Program.cs`, `src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj`
- Contracts: `specs/003-catalog-buildingblocks-tests/contracts/`
- Validation: `specs/003-catalog-buildingblocks-tests/quickstart.md`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add two test projects and register them in the solution. Mirror package versions from `tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj`.

- [X] T001 Create `tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj` targeting `net10.0` with `Microsoft.NET.Test.Sdk` 17.14.1, `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.4, `Microsoft.AspNetCore.Mvc.Testing` 10.0.1, NSubstitute, and a project reference to `src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj`
- [X] T002 [P] Create `tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj` targeting `net10.0` with the same test SDK/xUnit/runner versions (no NSubstitute), `Microsoft.AspNetCore.Mvc.Testing` 10.0.1, `MediatR` 12.5.0, `Carter` 10.0.0, and a project reference to `src/BuildingBlocks/BuildingBlock/BuildingBlock.csproj`
- [X] T003 Add `eShop.Catalog.Api.Tests` under `/tests/Services/` and `BuildingBlock.Tests` under `/tests/BuildingBlocks/` in `eShop.slnx` (do not remove `BuildBlock.Logging.Tests`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Testability seams and shared in-suite store so US1–US3 can compile and run without Postgres or Grafana.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Add `<InternalsVisibleTo Include="eShop.Catalog.Api.Tests" />` to `src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj`
- [X] T005 [P] Append `public partial class Program { }` at the end of `src/Services/eShop.Catalog.Api/Program.cs` (required for `WebApplicationFactory<Program>`; do not change logging or DI)
- [X] T006 Add in-suite session substitute in `tests/Services/eShop.Catalog.Api.Tests/Support/InMemoryDocumentSession.cs` that stores `Product` by `Id` with required fields `Id` (UUID, non-empty), `Name`, `Price`, `Description`, `ImageFile`, `Categories` (list of string); `Store` upserts; `LoadAsync<Product>(Guid)` returns the stored product; `SaveChangesAsync` is a no-op success
- [X] T007 Add `tests/Services/eShop.Catalog.Api.Tests/Support/CatalogApiFactory.cs` as `WebApplicationFactory<Program>` that sets non-empty dummy `ConnectionStrings:CatalogDb` (ValidateOnStart) and replaces `IDocumentSession` with `InMemoryDocumentSession` so no live Postgres or Grafana is used

**Checkpoint**: Foundation ready — test projects compile; factory and in-suite store exist; user story tests can be written

---

## Phase 3: User Story 1 - Prove Catalog can create a product (Priority: P1) 🎯 MVP

**Goal**: Isolated handler checks and service-level `POST /products` checks prove a valid create returns a new identity and stores matching product fields.

**Independent Test**: Grafana stopped. `dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj --filter FullyQualifiedName~CreateProduct`. Valid create returns a new `Id`; stored `Product` matches `Name`, `Price`, `Description`, `ImageFile`, `Categories`; two creates get different ids.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST. They should fail to compile before T004/T005, then fail or pass against current handler behavior — do not add new Catalog validation.**

- [X] T008 [P] [US1] Isolated tests in `tests/Services/eShop.Catalog.Api.Tests/CreateProductHandlerTests.cs`: construct `CreateProductHandler` with `InMemoryDocumentSession`; valid action returns new non-empty `Id`; stored `Product` matches `Name`, `Price`, `Description`, `ImageFile`, `Categories`; two `Handle` calls produce distinct ids (FR-001, FR-011). Do not assert rejection of empty name or negative price
- [X] T009 [P] [US1] Service-level tests in `tests/Services/eShop.Catalog.Api.Tests/CreateProductEndpointTests.cs` per `specs/003-catalog-buildingblocks-tests/contracts/create-product-api.md`: `POST /products` JSON `name`, `price`, `description`, `imageFile`, `categories` → `201 Created`, `Location` contains `/products/{id}`, body `id`; `LoadAsync` of that `Id` matches all required Product fields; two POSTs return distinct ids (FR-002, FR-010, FR-011)

### Implementation for User Story 1

- [X] T010 [US1] Run `CreateProductHandlerTests` and `CreateProductEndpointTests` in `tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj`. Change `src/Services/eShop.Catalog.Api/Products/CreateProduct/CreateProductHandler.cs` only if a test fails for missing identity, `Store`, or `SaveChangesAsync`. Do not add field validation. Do not add GET/PUT/DELETE product routes (FR-012)

**Checkpoint**: User Story 1 is independently testable (handler + POST /products, no Grafana)

---

## Phase 4: User Story 2 - Prove the shared building block Catalog relies on (Priority: P2)

**Goal**: Isolated BuildingBlock checks prove shared command handling and `AddAppCarter` request-entry registration without Catalog create-product or Grafana.

**Independent Test**: `dotnet test tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj` with Grafana stopped. Sample command dispatches; test Carter module route is not 404.

### Tests for User Story 2 ⚠️

- [X] T011 [P] [US2] Command handling tests in `tests/BuildingBlocks/BuildingBlock.Tests/CommandHandlingTests.cs` per `specs/003-catalog-buildingblocks-tests/contracts/buildingblock-api.md` §1: test-assembly `ICommand<T>` + `ICommandHandler<T,T>` registered with MediatR; `ISender.Send` returns the handler result (FR-003)
- [X] T012 [P] [US2] Request-entry tests in `tests/BuildingBlocks/BuildingBlock.Tests/AddAppCarterTests.cs` plus a test `ICarterModule` in `tests/BuildingBlocks/BuildingBlock.Tests/Support/TestCarterModule.cs` per `contracts/buildingblock-api.md` §2: `AddAppCarter` returns the same `IServiceCollection`; after `MapCarter()` the module route responds non-404 (FR-004)

### Implementation for User Story 2

- [X] T013 [US2] Run `tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj`. Change `src/BuildingBlocks/BuildingBlock/Cqrs/` or `src/BuildingBlocks/BuildingBlock/Extensions/CarterExtensions.cs` only if a test fails. Do not edit `src/BuildingBlocks/BuildBlock.Logging/` or `tests/BuildingBlocks/BuildBlock.Logging.Tests/` (FR-008)

**Checkpoint**: User Stories 1 and 2 both work independently

---

## Phase 5: User Story 3 - Run the suites independently and keep existing diagnostics checks green (Priority: P3)

**Goal**: Missing `ConnectionStrings:CatalogDb` fails startup with a named error. Isolated suites do not start Grafana. Existing logging tests still pass. No Loki/export production changes.

**Independent Test**: Grafana stopped. Catalog host without `CatalogDb` fails with `ConnectionStrings:CatalogDb` in the message. `dotnet test` on logging tests still passes. Diff has no compose/logging/Serilog edits.

### Tests for User Story 3 ⚠️

- [X] T014 [US3] Startup test in `tests/Services/eShop.Catalog.Api.Tests/CatalogStartupTests.cs`: `WebApplicationFactory` (or host) with missing or whitespace `ConnectionStrings:CatalogDb` fails start; exception message contains `ConnectionStrings:CatalogDb` (FR-007). Do not change `src/Services/eShop.Catalog.Api/DependencyInjection.cs` unless the current ValidateOnStart message is wrong

### Implementation for User Story 3

- [X] T015 [P] [US3] Run unchanged `tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj` and confirm 100% of previously passing tests still pass (FR-008, SC-004)
- [X] T016 [US3] Confirm `git diff` has no edits under `deploy/observability/`, `src/BuildingBlocks/BuildBlock.Logging/`, `tests/BuildingBlocks/BuildBlock.Logging.Tests/`, or Catalog `Serilog` / OTLP in `src/Services/eShop.Catalog.Api/appsettings.json` (FR-009)

**Checkpoint**: All user stories independently functional

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Prove the quickstart end-to-end with Grafana down.

- [X] T017 Run the full validation in `specs/003-catalog-buildingblocks-tests/quickstart.md` (`BuildingBlock.Tests`, `eShop.Catalog.Api.Tests`, `BuildBlock.Logging.Tests`) with the observability stack stopped
- [X] T018 Confirm isolated filter commands in `specs/003-catalog-buildingblocks-tests/quickstart.md` still match test class names in `tests/Services/eShop.Catalog.Api.Tests/` and `tests/BuildingBlocks/BuildingBlock.Tests/`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational — MVP
- **User Story 2 (Phase 4)**: Depends on Foundational only (BuildingBlock project from Setup). Does not need US1 tests to pass
- **User Story 3 (Phase 5)**: Depends on Foundational; T014 uses Catalog factory patterns from Phase 2; T015 is independent of US1/US2
- **Polish (Phase 6)**: Depends on the stories you intend to ship

### User Story Dependencies

- **User Story 1 (P1)**: After Phase 2 — Catalog tests only
- **User Story 2 (P2)**: After Phase 2 — BuildingBlock tests only; do not copy Catalog create-product scenarios
- **User Story 3 (P3)**: After Phase 2 — startup failure + logging regression + diff guard; can follow US1 factory but is independently testable

### Within Each User Story

- Tests MUST be written before any production handler/Carter change
- Seams (InternalsVisibleTo, `Program`) are Phase 2, not story implementation
- Run tests; production edits only if red for a real bug
- Do not add product GET/UPDATE/DELETE or new create-product rejection rules

### Parallel Opportunities

- T001 and T002 (different csproj files)
- T004 and T005 (csproj vs Program.cs)
- T008 and T009 after Phase 2 (handler tests vs endpoint tests)
- T011 and T012 (different BuildingBlock test files)
- T014 and T015 (new Catalog startup test vs existing logging suite)

---

## Parallel Example: User Story 1

```bash
# After Phase 2, different files:
Task: "Isolated tests in tests/Services/eShop.Catalog.Api.Tests/CreateProductHandlerTests.cs"
Task: "Service-level tests in tests/Services/eShop.Catalog.Api.Tests/CreateProductEndpointTests.cs"
```

## Parallel Example: User Story 2

```bash
Task: "CommandHandlingTests.cs in tests/BuildingBlocks/BuildingBlock.Tests/"
Task: "AddAppCarterTests.cs + Support/TestCarterModule.cs in tests/BuildingBlocks/BuildingBlock.Tests/"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Test projects + slnx
2. Complete Phase 2: InternalsVisibleTo, `Program`, in-suite session, factory
3. Complete Phase 3: Handler + POST /products tests
4. **STOP and VALIDATE**: `dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj --filter FullyQualifiedName~CreateProduct`
5. Demo create-product checks with Grafana down

### Incremental Delivery

1. Setup + seams → tests can compile
2. US1 → Catalog create-product MVP
3. US2 → BuildingBlock isolated checks
4. US3 → missing CatalogDb + logging still green + diff guard
5. T017–T018 → full quickstart

### Parallel Team Strategy

After Phase 2: Developer A on US1 Catalog tests; Developer B on US2 BuildingBlock tests. Do not both edit `eShop.Catalog.Api.csproj` / `Program.cs` after Phase 2.

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to spec user stories
- Product field constraints (quote): `Id` new non-empty UUID; two creates MUST differ; `Name`, `Price`, `Description`, `ImageFile`, `Categories` echoed; empty name / negative price are recorded, not newly rejected
- Do not add Testcontainers or live Postgres
- Do not start Grafana/Loki for these suites
- Stop at any checkpoint to validate the story independently
