# Tasks: Paginated Catalog Browse and Shared Item Traits

**Input**: Design documents from `/specs/005-catalog-page-entities/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included. Spec FR-013/FR-014 and workspace TDD require failing tests before production logic. Write tests first; they MUST fail (compile or assert) before the matching implementation task. Do not edit `BuildBlock.Logging` or `deploy/`. Do not add public delete or activate routes (FR-017). Do not commit unless the user asks.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- BuildingBlock production: `src/BuildingBlocks/BuildingBlock/`
- Catalog production: `src/Services/eShop.Catalog.Api/`
- Catalog tests: `tests/Services/eShop.Catalog.Api.Tests/`
- BuildingBlock tests: `tests/BuildingBlocks/BuildingBlock.Tests/`
- Logging tests (do not modify): `tests/BuildingBlocks/BuildBlock.Logging.Tests/`
- Contracts: `specs/005-catalog-page-entities/contracts/`
- Validation: `specs/005-catalog-page-entities/quickstart.md`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Reuse the existing Catalog and BuildingBlock test projects. No new csproj.

- [x] T001 Confirm `tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj` and `tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj` remain in `eShop.slnx` under `/tests/Services/` and `/tests/BuildingBlocks/`; do not add a new test project or change package versions

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Isolated browse must query the in-suite store without Postgres. This unblocks US1–US3 handler tests.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 Extend `tests/Services/eShop.Catalog.Api.Tests/Support/InMemoryDocumentSession.cs` so `CreateSession().Query<Product>()` returns an `IMartenQueryable<Product>` whose `Provider` and `Expression` come from the in-memory product list (research.md §4); keep existing `Store`, `LoadAsync<Product>(Guid)`, and `SaveChangesAsync`; do not add Testcontainers or a live `CatalogDb`

**Checkpoint**: Foundation ready — `Query<Product>()` works on the in-suite session; user story tests can be written

---

## Phase 3: User Story 1 - Browse a page of catalog products (Priority: P1) 🎯 MVP

**Goal**: Clients can `GET /products` with `pageNumber` and `pageSize` and receive that slice plus `totalCount`. Empty catalog or a page past the end is `200` with empty `items`. Invalid page values are `400`.

**Independent Test**: Grafana stopped. Seed several products in the in-suite store. `GET /products?pageNumber=1&pageSize=N` (N smaller than total) returns only N items and the total. A later empty page is `200` with `items=[]`. `pageNumber=0` or `pageSize=101` is `400`.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T003 [P] [US1] Isolated page-model tests in `tests/BuildingBlocks/BuildingBlock.Tests/PaginatedResultTests.cs` per `specs/005-catalog-page-entities/contracts/buildingblock-entities.md` §2: construct `PaginationRequest` and assert defaults `PageNumber` default `1` and `PageSize` default `10`; construct `PaginatedResult<T>` with `PageNumber`, `PageSize`, `TotalCount`, and `Items` without starting Catalog (FR-012, FR-014)
- [x] T004 [P] [US1] Isolated browse tests in `tests/Services/eShop.Catalog.Api.Tests/GetProductsHandlerTests.cs`: seed more products than one page; `Handle` with `PageNumber` ≥ `1` and `PageSize` ≥ `1` returns `Items.length` ≤ `PageSize` and `TotalCount` equal to the stored product count; empty store returns `Items=[]` and `TotalCount` `0`; page past the last product returns `Items=[]` and the same `TotalCount` (FR-002, FR-003)
- [x] T005 [P] [US1] Service-level browse tests in `tests/Services/eShop.Catalog.Api.Tests/GetProductsEndpointTests.cs` per `specs/005-catalog-page-entities/contracts/get-products-api.md`: `GET /products?pageNumber=1&pageSize=N` → `200` with `pageNumber`, `pageSize`, `totalCount`, `items`; omitted query uses defaults `1` and `10`; empty catalog or page past the end → `200` and `items=[]`; `pageNumber=0`, `pageSize=0`, and `pageSize=101` → `400 Bad Request` (FR-001, FR-004)

### Implementation for User Story 1

- [x] T006 [P] [US1] Add `PaginationRequest` in `src/BuildingBlocks/BuildingBlock/Models/PaginationRequest.cs` with `PageNumber` integer default `1` and `PageSize` integer default `10` (data-model.md PaginationRequest)
- [x] T007 [P] [US1] Add `PaginatedResult<T>` in `src/BuildingBlocks/BuildingBlock/Models/PaginatedResult.cs` with required `PageNumber`, `PageSize`, `TotalCount` (count of the default browse set, not the page length), and `Items` list of T (FR-012)
- [x] T008 [US1] Add `GetProductsQuery` (`IQuery<PaginatedResult<Product>>`), FluentValidation (`PageNumber` ≥ `1`; `PageSize` ≥ `1` and ≤ `100`), and `GetProductsHandler` in `src/Services/eShop.Catalog.Api/Products/GetProducts/GetProductsHandler.cs` that uses standard LINQ on `session.Query<Product>()` with `Skip((PageNumber - 1) * PageSize)` and `Take(PageSize)`; do not use Marten `ToListAsync`/`CountAsync` (research.md §4, FR-004)
- [x] T009 [P] [US1] Map `FluentValidation.ValidationException` to HTTP `400 Bad Request` in `src/BuildingBlocks/BuildingBlock/Extensions/EndpointExceptionExtension.cs` (keep existing `NotFoundException` → `404` `{ message }`)
- [x] T010 [US1] Add `GET /products` in `src/Services/eShop.Catalog.Api/Products/GetProducts/GetProductsEndpoint.cs` binding `[AsParameters] PaginationRequest` query names `pageNumber` and `pageSize`, sending `GetProductsQuery`, returning `200` + `PaginatedResult<Product>`, `.WithException()`, no free-text or category filters (FR-001, FR-017)
- [x] T011 [US1] Run `tests/BuildingBlocks/BuildingBlock.Tests/PaginatedResultTests.cs` and `tests/Services/eShop.Catalog.Api.Tests/GetProductsHandlerTests.cs` plus `GetProductsEndpointTests.cs` via `dotnet test` on those two csproj files; change only the US1 files above until they pass. Do not add delete/activate routes

**Checkpoint**: User Story 1 is independently testable (handler + GET /products, no Grafana)

---

## Phase 4: User Story 2 - Give every catalog product shared item traits (Priority: P2)

**Goal**: `Product` uses shared `BaseEntity` (`IAuditable`, `IDeletable`, `IActivable`). Create sets identity, `CreatedAt`/`LastModified` UTC, `IsActive` default `true`, `IsDeleted` default `false`. Update keeps `Id` and `CreatedAt` and moves `LastModified` forward. Default browse omits deleted. Get-by-id of deleted is not found; inactive not-deleted is returned.

**Independent Test**: Grafana stopped. Create a product and assert trait defaults. Update and assert `Id`/`CreatedAt` unchanged and `LastModified` later. Mark one product `IsDeleted=true` in the in-suite store: default page omits it; `GET /products/{id}` is 404. Inactive not-deleted get-by-id returns the product.

### Tests for User Story 2 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T012 [P] [US2] Isolated item-base tests in `tests/BuildingBlocks/BuildingBlock.Tests/BaseEntityTests.cs` per `contracts/buildingblock-entities.md` §1: a test type inherits `BaseEntity`; instance exposes `Id` (UUID), `CreatedAt`, `LastModified` (date-time UTC), `CreatedBy`, `LastModifiedBy` (string or null), `IsDeleted` boolean default `false`, `IsActive` boolean default `true`; no Catalog host (FR-008, FR-014)
- [x] T013 [P] [US2] Extend `tests/Services/eShop.Catalog.Api.Tests/CreateProductHandlerTests.cs` (do not rewrite existing identity/field tests): after a valid create, stored product has new non-empty `Id`, `CreatedAt` set, `LastModified` set (same instant as `CreatedAt` on create), `IsDeleted` default `false`, `IsActive` default `true`; `CreatedBy` and `LastModifiedBy` may be null (FR-010, FR-016)
- [x] T014 [P] [US2] Isolated update tests in `tests/Services/eShop.Catalog.Api.Tests/UpdateProductHandlerTests.cs`: successful update keeps `Id` and `CreatedAt` unchanged and sets `LastModified` to a later UTC value; `Name`, `Price`, `Description`, `ImageFile`, `Categories` match the command (FR-011)
- [x] T015 [P] [US2] Isolated get-by-id lifecycle tests in `tests/Services/eShop.Catalog.Api.Tests/GetByIdHandlerTests.cs`: missing `Id` throws `ProductNotFoundException`; `IsDeleted` `true` throws `ProductNotFoundException`; `IsActive` `false` and `IsDeleted` `false` returns the product (FR-006, FR-007)
- [x] T016 [US2] Extend `tests/Services/eShop.Catalog.Api.Tests/GetProductsHandlerTests.cs`: default browse set includes `IsDeleted = false` (active or inactive) and excludes `IsDeleted = true`; `TotalCount` is the not-deleted count; order is `LastModified` descending, then `Id` ascending; two identical `Handle` calls against an unchanged store return the same `Items` in the same order (FR-005, FR-018)

### Implementation for User Story 2

- [x] T017 [P] [US2] Add `IAuditable` in `src/BuildingBlocks/BuildingBlock/Models/IAuditable.cs` with `CreatedAt` and `LastModified` (`DateTimeOffset`, UTC) required, `CreatedBy` and `LastModifiedBy` string or null (empty until Catalog has an authenticated caller)
- [x] T018 [P] [US2] Add `IDeletable` in `src/BuildingBlocks/BuildingBlock/Models/IDeletable.cs` with `IsDeleted` boolean required, default `false`
- [x] T019 [P] [US2] Add `IActivable` in `src/BuildingBlocks/BuildingBlock/Models/IActivable.cs` with `IsActive` boolean required, default `true` (requested name `IActivable`)
- [x] T020 [US2] Add abstract `BaseEntity` in `src/BuildingBlocks/BuildingBlock/Models/BaseEntity.cs` implementing `IAuditable`, `IDeletable`, `IActivable` with `Id` UUID required plus the trait fields and defaults from T017–T019 (FR-008)
- [x] T021 [US2] Change `src/Services/eShop.Catalog.Api/Models/Product.cs` to inherit `BaseEntity` and remove the local `Id`; keep `Name`, `Price`, `Description`, `ImageFile`, `Categories` (FR-009)
- [x] T022 [P] [US2] In `src/Services/eShop.Catalog.Api/Products/CreateProduct/CreateProductHandler.cs` set identity, `IsActive` default `true`, and `IsDeleted` default `false`; `GenericRepository.Create` sets `CreatedAt` and `LastModified` to one `DateTimeOffset.UtcNow` value; do not change `POST /products` path or body (FR-010)
- [x] T023 [P] [US2] In `src/Services/eShop.Catalog.Api/Products/UpdateProduct/UpdateProductHandler.cs` keep `Id` and `CreatedAt` unchanged; `GenericRepository.Update` refreshes `LastModified`; do not change `PUT /products/{id}` path or body (FR-011)
- [x] T024 [P] [US2] In `src/Services/eShop.Catalog.Api/Products/GetById/GetByIdHandler.cs` throw `ProductNotFoundException` when the product is null or `IsDeleted` is `true` (FR-007)
- [x] T025 [US2] Add `.WithException()` on `GET /products/{id}` in `src/Services/eShop.Catalog.Api/Products/GetById/GetByIdEndpoint.cs` so missing/deleted returns `404` `{ message }` (contracts/get-product-by-id-api.md §2)
- [x] T026 [US2] Update `src/Services/eShop.Catalog.Api/Products/GetProducts/GetProductsHandler.cs` to filter `IsDeleted = false`, include inactive not-deleted products, and order by `LastModified` descending then `Id` ascending (FR-005, FR-018)
- [x] T027 [US2] Run `tests/BuildingBlocks/BuildingBlock.Tests/BaseEntityTests.cs` and `tests/Services/eShop.Catalog.Api.Tests/CreateProductHandlerTests.cs`, `UpdateProductHandlerTests.cs`, `GetByIdHandlerTests.cs`, `GetProductsHandlerTests.cs` via `dotnet test` on those two csproj files; change only the US2 files above until they pass. Do not add public delete or activate routes (FR-017)

**Checkpoint**: User Stories 1 and 2 both work independently

---

## Phase 5: User Story 3 - Prove Catalog search paths with automated checks (Priority: P3)

**Goal**: Service-level get-by-id covers found, missing, deleted, and inactive. Existing create-product and logging suites still pass. Isolated BuildingBlock and Catalog checks still run without Grafana.

**Independent Test**: Grafana stopped. `dotnet test` get-by-id endpoint tests: `200` for stored not-deleted (including inactive), `404` for missing or deleted. Create-product and `BuildBlock.Logging.Tests` still pass.

### Tests for User Story 3 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T028 [US3] Service-level get-by-id tests in `tests/Services/eShop.Catalog.Api.Tests/GetByIdEndpointTests.cs` per `specs/005-catalog-page-entities/contracts/get-product-by-id-api.md`: `GET /products/{id}` → `200` with `id`, `name`, `price`, `description`, `imageFile`, `categories`, `createdAt`, `lastModified`, `createdBy`, `lastModifiedBy`, `isDeleted` `false`, `isActive`; inactive not-deleted → `200`; missing `id` → `404` `{ message }`; `IsDeleted` `true` (seeded in the in-suite store) → `404` (FR-006, FR-007, FR-013)

### Implementation for User Story 3

- [x] T029 [US3] Run `GetByIdEndpointTests` in `tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj`. Change `src/Services/eShop.Catalog.Api/Products/GetById/` only if a test fails for missing `WithException` or deleted-as-not-found. Do not add a delete route (FR-017)
- [x] T030 [P] [US3] Run existing `CreateProductHandlerTests` and `CreateProductEndpointTests` in `tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj` and confirm they still pass (FR-016, SC-008)
- [x] T031 [P] [US3] Run unchanged `tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj` and confirm 100% of previously passing tests still pass (FR-016)
- [x] T032 [US3] Confirm `git diff` has no edits under `deploy/`, `src/BuildingBlocks/BuildBlock.Logging/`, `tests/BuildingBlocks/BuildBlock.Logging.Tests/`, or Catalog Serilog/OTLP settings in `src/Services/eShop.Catalog.Api/appsettings.json` (FR-015, FR-016)

**Checkpoint**: All user stories independently functional

---

## Phase 5b: User Story 1 search filters (Priority: P1)

**Goal**: `GET /products` can optionally search by name (contains, case-insensitive), exact price, or category (exact, case-insensitive). Combined filters AND. `totalCount` is the filtered set. Negative price is `400`.

**Independent Test**: Seed products with different names, prices, and categories. `GET /products?name=bike` returns only name matches. `?price=10` and `?category=outdoor` do the same. Combined query returns the intersection. Inactive/deleted matches stay omitted.

### Tests for search ⚠️

- [x] T036 [P] [US1] Isolated search tests in `tests/Services/eShop.Catalog.Api.Tests/GetProductsHandlerTests.cs`: name contains (case-insensitive); exact price; category match; combined filters AND; inactive/deleted matches omitted from the filtered total (FR-019)
- [x] T037 [P] [US1] Service-level search tests in `tests/Services/eShop.Catalog.Api.Tests/GetProductsEndpointTests.cs`: `GET /products?name=`, `?price=`, `?category=` return only matches; `price=-1` → `400` (FR-019, FR-020)

### Implementation for search

- [x] T038 [US1] Extend `GetProductsQuery` / `GetProductsValidator` in `src/Services/eShop.Catalog.Api/Products/GetProducts/GetProductsHandler.cs` with optional `Name`, `Price`, `Category`; reject `Price < 0`; apply filters after `FindAll()`
- [x] T039 [US1] Bind optional `name`, `price`, `category` on `GET /products` in `src/Services/eShop.Catalog.Api/Products/GetProducts/GetProductsEndpoint.cs`
- [x] T040 [US1] Run `GetProductsHandlerTests` and `GetProductsEndpointTests`; update `README.md` Catalog endpoints for browse/search

**Checkpoint**: Browse can page and search without Grafana

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Prove the quickstart end-to-end with Grafana down.

- [x] T033 Run the full validation in `specs/005-catalog-page-entities/quickstart.md` (`BuildingBlock.Tests`, `eShop.Catalog.Api.Tests`, `BuildBlock.Logging.Tests`) with the observability stack stopped
- [x] T034 Confirm isolated filter commands in `specs/005-catalog-page-entities/quickstart.md` still match test class names in `tests/Services/eShop.Catalog.Api.Tests/` and `tests/BuildingBlocks/BuildingBlock.Tests/`
- [x] T035 [P] Extend `tests/Services/eShop.Catalog.Api.Tests/OpenApiDocumentTests.cs` so `/openapi/v1.json` includes `GET /products` (keep existing `PUT /products/{id}` assertion)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational — MVP browse
- **User Story 2 (Phase 4)**: Depends on Foundational; T016/T026 assume US1 `GetProductsHandler` exists; T013 extends existing create tests
- **User Story 3 (Phase 5)**: Depends on Foundational; T028/T029 assume US2 get-by-id deleted behavior; T030–T032 are regression guards
- **Polish (Phase 6)**: Depends on the stories you intend to ship

### User Story Dependencies

- **User Story 1 (P1)**: After Phase 2 — page model + GET /products. Can page current `Product` without `BaseEntity`
- **User Story 2 (P2)**: After Phase 2; integrate with US1 browse to omit deleted and order by `LastModified`. Independently testable via create/update/get-by-id trait checks
- **User Story 3 (P3)**: After US2 get-by-id seams — service-level get-by-id + create/logging still green

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Shared types (`PaginationRequest`, `PaginatedResult<T>`, `BaseEntity`) before handlers
- Handlers before endpoints
- No public delete/activate routes
- Do not rewrite create-product tests; only add trait assertions (T013)

### Parallel Opportunities

- T003, T004, T005 after Phase 2 (three different test files)
- T006 and T007 (two pagination type files)
- T009 vs T008 (extension vs handler; T010 needs both)
- T012, T013, T014, T015 after Phase 2 / US1 handler file exists for T016
- T017, T018, T019 (three interface files)
- T022, T023, T024 after T021 (three handler files)
- T030 and T031 (create-product vs logging suites)

---

## Parallel Example: User Story 1

```bash
# After Phase 2, different files:
Task: "PaginatedResultTests.cs in tests/BuildingBlocks/BuildingBlock.Tests/"
Task: "GetProductsHandlerTests.cs in tests/Services/eShop.Catalog.Api.Tests/"
Task: "GetProductsEndpointTests.cs in tests/Services/eShop.Catalog.Api.Tests/"
```

## Parallel Example: User Story 2

```bash
Task: "BaseEntityTests.cs in tests/BuildingBlocks/BuildingBlock.Tests/"
Task: "CreateProductHandlerTests.cs trait asserts in tests/Services/eShop.Catalog.Api.Tests/"
Task: "UpdateProductHandlerTests.cs in tests/Services/eShop.Catalog.Api.Tests/"
Task: "GetByIdHandlerTests.cs in tests/Services/eShop.Catalog.Api.Tests/"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Confirm existing test projects
2. Complete Phase 2: `Query<Product>()` on the in-suite session
3. Complete Phase 3: Page types + GET /products
4. **STOP and VALIDATE**: `dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj --filter FullyQualifiedName~GetProducts`
5. Demo paginated browse with Grafana down

### Incremental Delivery

1. Setup + Query stub → browse tests can compile against the store
2. US1 → GET /products MVP
3. US2 → `BaseEntity` + create/update/get-by-id lifecycle
4. US3 → get-by-id HTTP checks + create/logging still green
5. T033–T035 → full quickstart + OpenAPI GET /products

### Parallel Team Strategy

After Phase 2: Developer A on US1 browse; Developer B can start US2 BuildingBlock entity tests (`BaseEntityTests` + interfaces) without touching `GetProductsEndpoint.cs`. Do not both edit `Product.cs` or `GetProductsHandler.cs`.

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to spec user stories
- Quoted constraints: `PageNumber` default `1`, reject `< 1`; `PageSize` default `10`, reject `< 1` or `> 100`; `TotalCount` is not-deleted count; `IsDeleted` default `false`; `IsActive` default `true`; `CreatedAt` unchanged on update; `LastModified` later on update; order `LastModified` desc then `Id` asc
- Do not add Testcontainers or live Postgres
- Do not start Grafana/Loki for these suites
- Do not commit unless the user asks
- Stop at any checkpoint to validate the story independently
