# Tasks: Paginated Catalog Browse, Shared Item Traits, and Search Sync

**Input**: Design documents from `/specs/005-catalog-page-entities/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included. Spec FR-013/FR-014/US3 and workspace TDD require failing tests before production logic. Write tests first; they MUST fail (compile or assert) before the matching implementation task. Do not edit `BuildBlock.Logging`. Do not add public delete or activate routes (FR-017). Do not dual-write Elasticsearch from create/update (FR-027). Do not commit unless the user asks.

**Organization**: Tasks are grouped by user story. T001–T077 and T079 are done (browse, search sync, BuildingBlock search extract). Remaining work is Tempo request traces (T080+) plus optional compose T078 / T087.

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
- Compose: `deploy/compose.yaml`, `deploy/compose.override.yaml`
- Contracts: `specs/005-catalog-page-entities/contracts/`
- Validation: `specs/005-catalog-page-entities/quickstart.md`

---

## Delivered (T001–T040)

Shared `BaseEntity` / page model, Marten-backed `GET /products` with name/cost/category filters, get-by-id deleted-as-not-found, and existing isolated/service-level suites are complete. Do not reopen those files except where a new task names them.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Packages and folders for the search-index seam. No new csproj.

- [x] T041 Add package references `Elastic.Clients.Elasticsearch` and `Confluent.Kafka` to `src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj`; do not add a new project
- [x] T042 [P] Create folder `src/Services/eShop.Catalog.Api/Search/` and test support file placeholder path `tests/Services/eShop.Catalog.Api.Tests/Support/InMemoryProductSearchStore.cs` (empty class allowed until T046)
- [x] T043 [P] Add `ElasticsearchOptions` (`Uri` required for compose, optional in test host) in `src/Services/eShop.Catalog.Api/Options/ElasticsearchOptions.cs` and `KafkaOptions` (`BootstrapServers`, consumer group) in `src/Services/eShop.Catalog.Api/Options/KafkaOptions.cs`; do not `ValidateOnStart` these so Catalog still starts when search is down (FR-024)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Search-store seam, 503 exception type, and in-memory test double. MUST complete before US1 handler swap.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T044 Add `SearchIndexUnavailableException` in `src/Services/eShop.Catalog.Api/Search/SearchIndexUnavailableException.cs` (Catalog service error, no stack in the public message)
- [x] T045 Add `IProductSearchStore` in `src/Services/eShop.Catalog.Api/Search/IProductSearchStore.cs` with async search that returns `PaginatedResult<Product>` using `PageNumber` default `1`, `PageSize` default `10`, reject caller-side `< 1` / `PageSize` `> 100` already handled by the query validator; throw `SearchIndexUnavailableException` when the index cannot be reached
- [x] T046 Implement `InMemoryProductSearchStore` in `tests/Services/eShop.Catalog.Api.Tests/Support/InMemoryProductSearchStore.cs`: seed/upsert/delete by `Id` UUID; query applies `IsActive` boolean required default `true` and `IsDeleted` boolean required default `false` (only active not-deleted documents); name is case-insensitive contains; `CostPrice`/`SewingCost`/`FabricCostAccessoriesCost`/`PackagingCost` exact match when supplied and reject is already on the query; `Category` case-insensitive exact match against one listed category; combined filters AND; `TotalCount` is the filtered set count not the page length
- [x] T047 Map `SearchIndexUnavailableException` to HTTP `503` `{ message }` (no stack) in `src/BuildingBlocks/BuildingBlock/Extensions/EndpointExceptionExtension.cs`; keep `NotFoundException` → `404` and `ValidationException` → `400`
- [x] T048 Register `IProductSearchStore` in `src/Services/eShop.Catalog.Api/DependencyInjection.cs` so the test host can replace it; do not register a Kafka hosted service yet; do not make Marten or Catalog startup depend on Elasticsearch

**Checkpoint**: Foundation ready — browse tests can target `IProductSearchStore` without Elasticsearch, Kafka, Kafka UI, Kibana, or Grafana

---

## Phase 3: User Story 1 - Browse a page of catalog products from the search index (Priority: P1) 🎯 MVP

**Goal**: `GET /products` reads the search index, not the catalog store. Empty/no-match/past-the-end stays `200` with empty `items`. Invalid page/cost stays `400`. Index unavailable is `503`, not an empty success page and not a Marten fallback. Get-by-id stays on the catalog store.

**Independent Test**: Grafana/Kafka/ES stopped. Seed the in-memory search store. `GET /products?pageNumber=1&pageSize=N` returns only that page from the index. A product only in the catalog store does not appear. Forced store failure → `503`; `GET /products/{id}` still `200`.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T049 [P] [US1] Isolated browse-from-index tests in `tests/Services/eShop.Catalog.Api.Tests/GetProductsHandlerTests.cs`: `Handle` uses `IProductSearchStore` only (not `ICatalogUnitOfWork.Products.FindAll()`); seed more products than one page; `Items.length` ≤ `PageSize`; `TotalCount` equals the filtered browse set; empty index returns `Items=[]` and `TotalCount` `0`; page past the end returns `Items=[]` and the same `TotalCount`; name contains (case-insensitive); exact `CostPrice`; category exact (case-insensitive); combined filters AND; omitted or blank name/category means no filter on that field; `CostPrice` `< 0` already rejected by validator (FR-001, FR-002, FR-003, FR-019, FR-020)
- [x] T050 [US1] Isolated outage test in `tests/Services/eShop.Catalog.Api.Tests/GetProductsHandlerTests.cs`: when `IProductSearchStore` throws `SearchIndexUnavailableException`, `Handle` does not query the catalog store (FR-023)
- [x] T051 [P] [US1] Service-level browse tests in `tests/Services/eShop.Catalog.Api.Tests/GetProductsEndpointTests.cs` per `specs/005-catalog-page-entities/contracts/get-products-api.md`: `GET /products` → `200` from the in-memory index; a catalog-store-only product MUST NOT appear in `items`; `pageNumber=0`, `pageSize=101`, `costPrice=-1` → `400`; search store unavailable → `503` `{ message }` and not `200` with `items=[]`; `GET /products/{id}` for a stored active not-deleted product still `200` (FR-023, FR-024, SC-011, SC-012)

### Implementation for User Story 1

- [x] T052 [US1] Change `GetProductsHandler` in `src/Services/eShop.Catalog.Api/Products/GetProducts/GetProductsHandler.cs` to call `IProductSearchStore` only; keep existing query fields (`Name`, `CostPrice`, `SewingCost`, `FabricCostAccessoriesCost`, `PackagingCost`, `Category`, `OrderBy`, `OrderDirection`); validator stays `PageNumber` ≥ `1`, `PageSize` ≥ `1` and ≤ `100`, each supplied cost `≥ 0`; do not write the search index from this handler (FR-001, FR-019, FR-027)
- [x] T053 [US1] Update `GET /products` in `src/Services/eShop.Catalog.Api/Products/GetProducts/GetProductsEndpoint.cs` to `.Produces(StatusCodes.Status503ServiceUnavailable)` while keeping `200` and `400`; `.WithException()` already maps 503 from T047 (contracts/get-products-api.md §3)
- [x] T054 [US1] Wire the test host in `tests/Services/eShop.Catalog.Api.Tests/` (existing `WebApplicationFactory` / fixture) to use `InMemoryProductSearchStore` for `IProductSearchStore`; do not start Elasticsearch
- [x] T055 [US1] Run `GetProductsHandlerTests` and `GetProductsEndpointTests` in `tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj`; change only US1 files above until they pass. Get-by-id, create, and update must still pass

**Checkpoint**: User Story 1 is independently testable (index-backed GET /products + 503, no Grafana/Kafka/ES)

---

## Phase 4: User Story 2 - Shared item traits (Priority: P2)

**Goal**: Already delivered. Product still inherits `BaseEntity`. Create/update public paths unchanged. Get-by-id treats deleted/inactive as not found.

**Independent Test**: Existing `BaseEntityTests`, `CreateProductHandlerTests`, `UpdateProductHandlerTests`, `GetByIdHandlerTests` still pass with Grafana stopped.

- [x] T056 [US2] Re-run `tests/BuildingBlocks/BuildingBlock.Tests/BaseEntityTests.cs` and Catalog create/update/get-by-id handler tests in `tests/Services/eShop.Catalog.Api.Tests/`; do not change `POST /products` or `PUT /products/{id}` paths or bodies (FR-010, FR-011, FR-016)

**Checkpoint**: User Stories 1 and 2 both work independently

---

## Phase 5: User Story 4 - Keep search in step with catalog writes (Priority: P2)

**Goal**: Create/update return after the catalog-store commit. Debezium snapshots then streams `public.mt_doc_product` onto topics prefixed `eshop.catalog.sync`. Catalog indexer upserts/deletes Elasticsearch `catalog-products`. Kafka UI filters that prefix. Kibana on port 5601 shows documents. Search metrics and indexer failure logs exist. No dual-write. No per-product success log.

**Independent Test**: Isolated indexer tests apply Debezium-shaped events to the in-memory store. Compose (optional for this phase’s isolated slice): create returns immediately; within 5 seconds search and Kibana show the product.

### Tests for User Story 4 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T057 [P] [US4] Isolated indexer tests in `tests/Services/eShop.Catalog.Api.Tests/ProductSearchIndexerTests.cs`: `op` `c`/`r`/`u` with after-image `IsActive` `true` and `IsDeleted` `false` upserts document id = `Product.Id`; `op` `d` or after-image `IsDeleted` `true` or `IsActive` `false` deletes the document; stale `Version` (`LastModified` UTC ticks) MUST NOT overwrite a newer document; a failing apply writes an error log and MUST NOT write a per-product success log (data-model.md SearchSyncEvent, FR-032)
- [x] T058 [P] [US4] Isolated metrics tests in `tests/Services/eShop.Catalog.Api.Tests/CatalogSearchMetricsTests.cs` per `specs/005-catalog-page-entities/contracts/search-observability.md`: successful search records histogram `catalog.search.duration`; index unavailable increments counter `catalog.search.errors`; gauge `catalog.search.sync.lag_seconds` can be read (0 when Kafka is not running) (FR-031, FR-032)
- [x] T059 [P] [US4] Isolated create-does-not-index test in `tests/Services/eShop.Catalog.Api.Tests/CreateProductHandlerTests.cs`: after a valid create, `InMemoryProductSearchStore` is unchanged unless an indexer event is applied (FR-025, FR-027)

### Implementation for User Story 4

- [x] T060 [P] [US4] Add `ElasticsearchProductSearchStore` in `src/Services/eShop.Catalog.Api/Search/ElasticsearchProductSearchStore.cs` targeting logical index `catalog-products`; document id = `Product.Id`; fields `Name`, `Amount`, `SewingCost`, `FabricCostAccessoriesCost`, `PackagingCost`, `CostPrice`, `Description`, `ImageFile`, `Categories` (keyword), `CreatedAt`, `LastModified`, actor fields, `IsDeleted`, `IsActive`; external version = `LastModified` UTC ticks; connection failures throw `SearchIndexUnavailableException` (data-model.md ProductSearchDocument)
- [x] T061 [US4] Add `ProductSearchIndexerHostedService` in `src/Services/eShop.Catalog.Api/Search/ProductSearchIndexerHostedService.cs` that consumes `eshop.catalog.sync.public.mt_doc_product`; map Debezium `c`/`r`/`u`/`d` as in T057; register only when `KafkaOptions.BootstrapServers` is configured; Catalog MUST start if Kafka is missing (FR-024, FR-027)
- [x] T062 [P] [US4] Add meter `eShop.Catalog.Search` instruments `catalog.search.duration`, `catalog.search.errors`, `catalog.search.sync.lag_seconds` via `WithMetrics` in `src/BuildingBlocks/BuildingBlock/Extensions/OpenTelemetryExtensions.cs` and record them from `GetProductsHandler` / indexer in `src/Services/eShop.Catalog.Api/`; reuse `LoggingAndTelemetryBehavior` for failed-request logs (request name, duration, stack in the message) (FR-021, FR-031)
- [x] T063 [US4] Enable `wal_level=logical` on `catalogdb` in `deploy/compose.override.yaml`; Catalog.Api MUST NOT `depends_on` elasticsearch, kafka, kafka-ui, or kibana
- [x] T064 [P] [US4] Add Kafka (KRaft, no ZooKeeper) and Debezium Connect services to `deploy/compose.yaml` and `deploy/compose.override.yaml`; Connect internal topics `eshop.catalog.sync.connect-configs`, `eshop.catalog.sync.connect-offsets`, `eshop.catalog.sync.connect-status`, `eshop.catalog.sync.schema-history`; connector `topic.prefix=eshop.catalog.sync`, `snapshot.mode=initial`, table `public.mt_doc_product` (FR-028, FR-029)
- [x] T065 [US4] Add `provectuslabs/kafka-ui` to `deploy/compose.yaml` and `deploy/compose.override.yaml` on a documented local HTTP port pointed at in-stack Kafka; no auth (local-dev); Catalog must start if Kafka UI is stopped (FR-030)
- [x] T066 [US4] Add official Kibana 8 (same major as Elasticsearch 8) to `deploy/compose.yaml` and `deploy/compose.override.yaml` published on host port `5601`, `ELASTICSEARCH_HOSTS=http://elasticsearch:9200`, no auth; data view `catalog-products`; Catalog and `dotnet test` MUST NOT require Kibana (FR-033, SC-020)
- [x] T067 [US4] Add Elasticsearch 8 single-node (`xpack.security.enabled=false` local-dev) to `deploy/compose.yaml` and `deploy/compose.override.yaml`; in-stack URI for Catalog `Elasticsearch__Uri`; Catalog must start if Elasticsearch is down
- [x] T068 [US4] Register production `IProductSearchStore` → `ElasticsearchProductSearchStore` in `src/Services/eShop.Catalog.Api/DependencyInjection.cs` when `ElasticsearchOptions.Uri` is set; test host keeps `InMemoryProductSearchStore`
- [x] T069 [US4] Run `ProductSearchIndexerTests`, `CatalogSearchMetricsTests`, and existing create/get-by-id tests in `tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj`; do not start compose for this command

**Checkpoint**: User Story 4 isolated slice is independently testable; compose UIs are defined

---

## Phase 6: User Story 3 - Prove Catalog search paths with automated checks (Priority: P3)

**Goal**: Isolated and service-level checks cover get-by-id, index-backed browse, 503 outage, indexer mapping, and metrics. Existing create-product and logging suites still pass. No Grafana/Kafka/ES required for `dotnet test`.

**Independent Test**: With log viewer, Kafka, Elasticsearch, Kafka UI, and Kibana stopped, run BuildingBlock + Catalog + logging test projects. Create-product and logging still pass.

### Tests for User Story 3 ⚠️

- [x] T070 [P] [US3] Confirm service-level get-by-id in `tests/Services/eShop.Catalog.Api.Tests/GetByIdEndpointTests.cs` still covers `200` for stored active not-deleted, `404` for missing/deleted/inactive per `contracts/get-product-by-id-api.md` (FR-006, FR-007, FR-013)
- [x] T071 [P] [US3] Extend `tests/Services/eShop.Catalog.Api.Tests/GetProductsEndpointTests.cs` so index-backed search (name/cost/category) and `503` outage stay in the suite after US4 wiring (FR-013, FR-023)
- [x] T072 [US3] Run existing `CreateProductHandlerTests` and `CreateProductEndpointTests` in `tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj` and confirm they still pass (FR-016, SC-008)

### Implementation for User Story 3

- [x] T073 [P] [US3] Run unchanged `tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj` and confirm previously passing tests still pass (FR-016)
- [x] T074 [US3] Confirm `git diff` has no edits under `src/BuildingBlocks/BuildBlock.Logging/` or `tests/BuildingBlocks/BuildBlock.Logging.Tests/` (FR-016)

**Checkpoint**: All user stories independently functional under `dotnet test`

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Operator compose path and docs. Does not block `dotnet test`.

- [x] T075 Update Catalog run notes in `README.md` with `GET /products` (search index), `503` when the index is down, Kafka UI prefix `eshop.catalog.sync`, and Kibana `http://localhost:5601` / `catalog-products`
- [x] T076 [P] Align `specs/005-catalog-page-entities/quickstart.md` compose service names with `deploy/compose.yaml` (`catalogdb`, `kafka`, `connect`, `elasticsearch`, `kibana`, `kafka-ui`, `catalogapi`)
- [x] T077 Run the `dotnet test` slice in `specs/005-catalog-page-entities/quickstart.md` (`BuildingBlock.Tests`, `eShop.Catalog.Api.Tests`, `BuildBlock.Logging.Tests`) with Grafana, Kafka, Elasticsearch, Kafka UI, and Kibana stopped
- [ ] T078 Optional operator check from `specs/005-catalog-page-entities/quickstart.md`: compose up search-sync; create-product returns before search; within 5 seconds `GET /products` and Kibana show the product; Kafka UI lists only `eshop.catalog.sync*` search-sync topics (SC-014, SC-016, SC-020)
- [x] T079 Move reusable Elasticsearch search types to `src/BuildingBlocks/BuildingBlock/Search/` (`ISearchStore<T>`, `SearchQuery`, stores, `SearchIndexer<T>`). Catalog keeps Product query mapping, index `catalog-products`, topic `eshop.catalog.sync.public.mt_doc_product`, and `catalog.search.*` meters. Isolated checks in `tests/BuildingBlocks/BuildingBlock.Tests/SearchStoreTests.cs`

---

## Phase 8: User Story 4 increment — Grafana Tempo request traces (Priority: P2)

**Goal**: After Catalog handles at least one public request with the log viewer stack running, Grafana **Explore → Tempo → Search** shows that request’s Catalog waterfall. Service name `eShop.Catalog.Api`. Parent = ASP.NET HTTP span; child = `eShop.Application` MediatR span when the pipeline runs. A Tempo service graph is not required. Catalog startup and `dotnet test` MUST NOT require Tempo (FR-034, SC-021).

**Independent Test**: Isolated `ActivityListener` / `AddAppOpenTelemetry` checks pass with Grafana stopped. Optional compose: start `lgtm` + `catalogapi`, send one request, Tempo Explore shows the waterfall.

### Tests for Tempo traces ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T080 [US4] Isolated failing test in `tests/BuildingBlocks/BuildingBlock.Tests/RequestTraceTests.cs`: after `AddAppOpenTelemetry("eShop.Catalog.Api")`, `new ActivitySource("eShop.Application").StartActivity(...)` is not null (OpenTelemetry MUST listen to source `eShop.Application`). Do not start Grafana or Tempo. MUST fail until T082 (contracts/search-observability.md §4, research.md §14)
- [x] T081 [P] [US4] Isolated test in `tests/BuildingBlocks/BuildingBlock.Tests/LoggingAndTelemetryBehaviorTests.cs`: with an `ActivityListener` on source `eShop.Application`, `Handle` records a span display name `MediatR {RequestName}` and tag `mediatr.request_type`. Do not start Tempo (data-model.md Request trace)

### Implementation for Tempo traces

- [x] T082 [US4] Add `.AddSource("eShop.Application")` in `src/BuildingBlocks/BuildingBlock/Extensions/OpenTelemetryExtensions.cs` next to `AddSource(applicationName)` so MediatR spans export with the ASP.NET parent. Do not add a Tempo service-graph or span-metrics pipeline (FR-034)
- [x] T083 [P] [US4] Set `OTEL_EXPORTER_OTLP_ENDPOINT=http://lgtm:4318` on `catalogapi` in `deploy/compose.override.yaml` so traces and metrics use the same in-stack LGTM collector as the Serilog log sink (`http://lgtm:4318`). Do not add `depends_on: lgtm`. Catalog MUST still start if LGTM is down (FR-034)
- [x] T084 [P] [US4] Update the LGTM section in `README.md`: remove “Catalog exports logs, not full traces, so Tempo can be empty”; document Grafana **Explore → Tempo → Search** by service name `eShop.Catalog.Api` after one public request. Do not require Tempo for Catalog startup

**Checkpoint**: Isolated RequestTrace + logging-behavior tests pass. Tempo still optional for `dotnet test`

---

## Phase 9: Tempo polish

**Purpose**: Prove suites still pass and document the operator Tempo path. Does not block isolated `dotnet test`.

- [x] T085 Confirm `git diff` has no edits under `src/BuildingBlocks/BuildBlock.Logging/` or `tests/BuildingBlocks/BuildBlock.Logging.Tests/` (FR-016)
- [x] T086 Run the `dotnet test` slice in `specs/005-catalog-page-entities/quickstart.md` (`BuildingBlock.Tests`, `eShop.Catalog.Api.Tests`, `BuildBlock.Logging.Tests`) with Grafana, Tempo, Kafka, Elasticsearch, Kafka UI, and Kibana stopped
- [ ] T087 Optional operator Tempo check from `specs/005-catalog-page-entities/quickstart.md`: `up -d --wait lgtm catalogapi`; one public Catalog request; Grafana Explore → Tempo → Search `eShop.Catalog.Api` shows the request waterfall; service graph not required (SC-021)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS US1/US4 handler and indexer work
- **User Story 1 (Phase 3)**: Depends on Foundational — index-backed browse MVP
- **User Story 2 (Phase 4)**: Verification only; already delivered
- **User Story 4 (Phase 5)**: Depends on Foundational; indexer/metrics assume `IProductSearchStore` from Phase 2; compose can start after T063–T067
- **User Story 3 (Phase 6)**: Depends on US1 (and US4 isolated tests); regression guards
- **Polish (Phase 7)**: Depends on the stories you intend to ship
- **Tempo traces (Phase 8)**: Independent of Kafka/ES; depends only on existing `AddAppOpenTelemetry` / `LoggingAndTelemetryBehavior`
- **Tempo polish (Phase 9)**: Depends on T082–T084

### User Story Dependencies

- **User Story 1 (P1)**: After Phase 2 — browse from in-memory index + 503. No Kafka required
- **User Story 2 (P2)**: Already delivered; T056 is a regression run
- **User Story 4 (P2)**: After Phase 2; can start indexer tests in parallel with US1 tests (different files). Compose services can be added in parallel with each other
- **User Story 3 (P3)**: After US1 endpoint tests exist; proves the suites still pass
- **User Story 4 Tempo increment**: After T079; T080 fails first; T083/T084 can run in parallel with T082 (different files)

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Search store interface before handler swap
- Handler before endpoint 503 metadata
- Isolated indexer before compose
- No public delete/activate routes
- Create/update MUST NOT write Elasticsearch

### Parallel Opportunities

- T042 and T043 after T041
- T049, T050, T051 after Phase 2 (same test project, different methods/files — T049/T050 share `GetProductsHandlerTests.cs` so do not mark those two as parallel with each other if one file lock; T051 is a different file)
- T057, T058, T059 after Phase 2 (three test files)
- T064, T065, T066, T067 (four compose service additions; coordinate in the same two yaml files — treat as sequential if one editor, [P] if split by service with care)
- T072 and T073 (Catalog create vs logging suite)
- T081 after or beside T080 (different test file)
- T083 and T084 after T082 starts (compose + README; different files)

---

## Parallel Example: User Story 1

```bash
# After Phase 2 (different files):
Task: "GetProductsEndpointTests 503/index source in tests/Services/eShop.Catalog.Api.Tests/GetProductsEndpointTests.cs"
# Handler tests in one file — do not split T049 and T050 across two people
Task: "GetProductsHandlerTests index + outage in tests/Services/eShop.Catalog.Api.Tests/GetProductsHandlerTests.cs"
```

## Parallel Example: User Story 4

```bash
Task: "ProductSearchIndexerTests.cs in tests/Services/eShop.Catalog.Api.Tests/"
Task: "CatalogSearchMetricsTests.cs in tests/Services/eShop.Catalog.Api.Tests/"
Task: "CreateProductHandlerTests no-index assert in tests/Services/eShop.Catalog.Api.Tests/"
```

## Parallel Example: Tempo traces (Phase 8)

```bash
# After T080 is written (different files):
Task: "LoggingAndTelemetryBehaviorTests span assert in tests/BuildingBlocks/BuildingBlock.Tests/LoggingAndTelemetryBehaviorTests.cs"
Task: "OTEL_EXPORTER_OTLP_ENDPOINT on catalogapi in deploy/compose.override.yaml"
Task: "Tempo Explore docs in README.md"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: packages and options
2. Complete Phase 2: `IProductSearchStore` + in-memory double + 503 mapping
3. Complete Phase 3: `GET /products` reads the index; outage is 503
4. **STOP and VALIDATE**: `dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj --filter FullyQualifiedName~GetProducts`
5. Demo index-backed browse with Grafana/Kafka/ES down

### Incremental Delivery

1. Setup + search-store seam → browse tests can compile
2. US1 → GET /products from index + 503 (MVP)
3. US2 regression → traits still green
4. US4 isolated indexer + metrics → then compose Kafka/ES/Kafka UI/Kibana
5. US3 → full `dotnet test` still green
6. T075–T078 → docs + optional compose freshness
7. T080–T087 → Tempo request-trace waterfall (FR-034)

### Parallel Team Strategy

After Phase 2: Developer A on US1 handler/endpoint; Developer B on US4 indexer tests + compose (avoid both editing `GetProductsHandler.cs` or `DependencyInjection.cs` at the same time).

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to spec user stories US1–US4
- Quoted constraints: `PageNumber` default `1`, reject `< 1`; `PageSize` default `10`, reject `< 1` or `> 100`; `TotalCount` is filtered set count; `IsDeleted` default `false`; `IsActive` default `true`; topic prefix `eshop.catalog.sync`; index `catalog-products`; Kibana port `5601`; freshness 5 seconds under normal sync; no per-product success log; Tempo service name `eShop.Catalog.Api`; activity source `eShop.Application`; OTLP endpoint `http://lgtm:4318`; no Tempo service graph
- Do not add Testcontainers for `dotnet test`
- Do not start Grafana/Loki/Tempo/Kafka/ES/Kafka UI/Kibana for isolated or service-level suites
- Do not commit unless the user asks
- Stop at any checkpoint to validate the story independently
