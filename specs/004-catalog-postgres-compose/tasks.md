# Tasks: Host Local Catalog Store and Catalog.Api

**Input**: Design documents from `/specs/004-catalog-postgres-compose/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included. Plan, quickstart, and workspace TDD rules require Red (contract checks that fail for the right reason) then Green (compose healthcheck / Dockerfile / catalogapi / Marten AutoCreate). No new test project and no Testcontainers. Isolated xUnit suites must keep passing with this compose stopped (FR-012). Host `dotnet run` is not an acceptance check (FR-015).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- Compose: `deploy/compose.yaml` (images + network + volume names), `deploy/compose.override.yaml` (service config, local build)
- Catalog image: `src/Services/eShop.Catalog.Api/Dockerfile`
- Catalog Marten: `src/Services/eShop.Catalog.Api/DependencyInjection.cs`
- Do not change: `src/BuildingBlocks/BuildBlock.Logging/`, create-product routes, `lgtm` Loki activation behavior
- Isolated tests (must still pass, compose down): `tests/Services/eShop.Catalog.Api.Tests/`, `tests/BuildingBlocks/BuildingBlock.Tests/`
- Contracts: `specs/004-catalog-postgres-compose/contracts/catalog-postgres.md`
- Validation: `specs/004-catalog-postgres-compose/quickstart.md`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the local stack is one compose pair under `deploy/`, Catalog already has the store setting key, and the Docker context will not leak local secrets.

- [X] T001 Use directory `deploy/` for the single compose pair (`compose.yaml` + `compose.override.yaml`); do not keep `deploy/catalog/` or `deploy/observability/` compose files. Catalog Dockerfile belongs at `src/Services/eShop.Catalog.Api/Dockerfile` (US2), not under `deploy/`
- [X] T002 [P] Keep `lgtm` in that pair in `deploy/compose.yaml` / `deploy/compose.override.yaml` (FR-010: start `catalogdb` without requiring `lgtm`; FR-011: keep Loki ports `3000`/`4317`/`4318` only)
- [X] T003 [P] Confirm Catalog already binds key `ConnectionStrings:CatalogDb` in `src/Services/eShop.Catalog.Api/Options/DatabaseOptions.cs` (`SectionName` MUST be `ConnectionStrings`; `CatalogDb` required non-empty) and that `src/Services/eShop.Catalog.Api/Program.cs` loads optional `appsettings.Development.local.json` in Development (do not invent a new configuration key; host settings files are not the documented success path)
- [X] T004 [P] Confirm `.dockerignore` excludes `**/appsettings.*.local.json`, `**/bin`, `**/obj`, and does **not** exclude `src/` or `src/Services/eShop.Catalog.Api/Dockerfile`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared local catalog store identity so US1–US3 use the same engine, database name, role, password, ports, and volume.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T005 Put `catalogdb` in `deploy/compose.yaml` / `deploy/compose.override.yaml`: Engine MUST be `PostgreSQL 16 (pinned image)` (current pin `postgres:16.15`); `container_name` `eshop_catalog_db`; Database name MUST be `catalog`; Role MUST be `catalog`; Password MUST be `Local default catalog; labeled local-development only`; Published host MUST be `localhost`, port `5432` (`5432:5432`); Data volume MUST be `catalog-pgdata` mounted at `/var/lib/postgresql/data`; `restart: always`. Keep `lgtm` in the same files. Do **not** add a `catalogdb` `healthcheck` yet (US1). Do **not** add `catalogapi` yet (US2). Do **not** use `down -v` as the documented stop

**Checkpoint**: Foundation ready — store identity exists; Ready wait, Catalog image, and Catalog.Api wiring are user-story work

---

## Phase 3: User Story 1 - Start a local catalog store for Catalog (Priority: P1) 🎯 MVP

**Goal**: One documented command starts a dedicated catalog store and blocks until Ready. No host Postgres install. No Catalog.Api and no log viewer required.

**Independent Test**: Log viewer stopped. Catalog.Api stopped. `podman compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait catalogdb` (or Docker equivalent) returns only after `pg_isready -U catalog -d catalog` succeeds. Published local store address `Host=localhost;Port=5432;Database=catalog;Username=catalog;Password=catalog` opens. Grafana/Loki and `catalogapi` are not started.

### Tests for User Story 1 ⚠️

> **NOTE: Run these checks FIRST against current `deploy/compose.override.yaml` (no catalogdb healthcheck). They must fail or be racy for the right reason before Green.**

- [X] T006 [US1] Red-phase contract §1–§2 in `specs/004-catalog-postgres-compose/contracts/catalog-postgres.md` against `deploy/compose.yaml` started with `up -d catalogdb` (no `--wait`): record that Ready (`pg_isready -U catalog -d catalog`) is not guaranteed before the command returns, and that `lgtm` and `catalogapi` are not required

### Implementation for User Story 1

- [X] T007 [US1] Add compose `healthcheck` on `catalogdb` in `deploy/compose.override.yaml` that succeeds only when `pg_isready -U catalog -d catalog` succeeds (interval/timeout/retries suitable for first start under 2 minutes after image pull). Do not publish extra ports. Do not add Catalog.Api yet. Keep `deploy/compose.yaml` as image + network + volume names only
- [X] T008 [US1] Change operator start comments in `deploy/compose.yaml` to `podman compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait catalogdb` (and Docker equivalent). Document **two views of the same store**: published local store address MUST be `Host=localhost;Port=5432;Database=catalog;Username=catalog;Password=catalog`; in-stack store address MUST be `Host=catalogdb;Port=5432;Database=catalog;Username=catalog;Password=catalog` (same database name and local credentials; host/port differ by view). Credentials MUST be labeled local-development defaults not production secrets; start MUST NOT require `lgtm` or `catalogapi`
- [X] T009 [US1] Green-phase: `up -d --wait catalogdb` then pass contract §1–§2 in `specs/004-catalog-postgres-compose/contracts/catalog-postgres.md`: command returns 0 only when Ready; `pg_isready -h localhost -p 5432 -U catalog -d catalog` succeeds; database name MUST be `catalog`; `lgtm` and `catalogapi` remain stopped

**Checkpoint**: User Story 1 is independently testable (store Ready after `--wait`, no Catalog.Api and no Grafana required)

---

## Phase 4: User Story 2 - Run Catalog.Api from the same compose pair (Priority: P2)

**Goal**: Catalog.Api is a compose service in the same pair. Documented Catalog run is `up -d --wait catalogapi` (starts the store if needed, not `lgtm`). Create-product against `http://localhost:5000` persists in this Postgres. Host Catalog is not required.

**Independent Test**: `up -d --wait catalogapi` returns only after `catalogdb` is healthy and Catalog is listening. `POST http://localhost:5000/products` with required body `name`, `price`, `description`, `imageFile`, `categories` → `201` and non-empty `id`; that `id` exists in Marten table `mt_doc_product` (confirm name at implement time). Do not use `dotnet run`. `lgtm` may stay stopped.

### Tests for User Story 2 ⚠️

> **NOTE: These checks MUST fail for the right reason before Green. Do not add GET/PUT/DELETE product routes. Do not use host `dotnet run` as the proof.**

- [X] T010 [US2] Red-phase contract §3 in `specs/004-catalog-postgres-compose/contracts/catalog-postgres.md`: with no `catalogapi` service and no `src/Services/eShop.Catalog.Api/Dockerfile`, `up -d --wait catalogapi` fails. Record that the documented Catalog run does not exist until T011–T013

### Implementation for User Story 2

- [X] T011 [P] [US2] Add multi-stage `src/Services/eShop.Catalog.Api/Dockerfile` (`mcr.microsoft.com/dotnet/sdk:10.0` build → `mcr.microsoft.com/dotnet/aspnet:10.0` runtime). Build context MUST be the repository root so `src/BuildingBlocks/BuildingBlock/` and `src/BuildingBlocks/BuildBlock.Logging/` restore. Runtime MUST listen on container port `8080`. Do not copy `appsettings.*.local.json`. Do not add a new Catalog HTTP health route
- [X] T012 [US2] Add `catalogapi` image `eshop-catalog-api:local` to `deploy/compose.yaml` (services + network `eshop` only). Do not put env/ports/build in this file. Documented Catalog start comment MUST be `up -d --wait catalogapi` (starts `catalogdb` if needed; MUST NOT require `lgtm`)
- [X] T013 [US2] Configure `catalogapi` in `deploy/compose.override.yaml`: `container_name` `eshop_catalog_api`; `build.context` `..` and `build.dockerfile` `src/Services/eShop.Catalog.Api/Dockerfile`; inject `ConnectionStrings__CatalogDb` with in-stack value `Host=catalogdb;Port=5432;Database=catalog;Username=catalog;Password=catalog` (key MUST remain `ConnectionStrings:CatalogDb`; MUST NOT use `Host=localhost`); Catalog local URL mapping MUST be `5000:8080` (`http://localhost:5000`, HTTPS not published); `depends_on` `catalogdb` `condition: service_healthy` and MUST NOT depend on `lgtm`; healthcheck TCP/listen on container `8080`; set `Serilog__WriteTo__1__Args__endpoint=http://lgtm:4318` (in-stack log-viewer address); `ASPNETCORE_ENVIRONMENT=Development` is allowed. Host settings files MUST NOT be the documented success path
- [X] T014 [US2] Red-phase persistence (store Ready, `catalogapi` up, Marten AutoCreate still default in `src/Services/eShop.Catalog.Api/DependencyInjection.cs`): `POST http://localhost:5000/products` per `specs/004-catalog-postgres-compose/contracts/catalog-postgres.md` §4 must fail (missing document table / schema), not return 201 with a row in `mt_doc_product`
- [X] T015 [US2] In `src/Services/eShop.Catalog.Api/DependencyInjection.cs` `AddMarten` `StoreOptions`, set `options.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate`. Do **not** call `ApplyAllDatabaseChangesOnStartup()`. Do not change create-product handler, routes, or `BuildBlock.Logging`
- [X] T016 [US2] Green-phase contract §3–§4 in `specs/004-catalog-postgres-compose/contracts/catalog-postgres.md`: `up -d --wait catalogapi` returns 0 only when the store is Ready and Catalog is listening; `lgtm` not required; `POST http://localhost:5000/products` JSON required fields `name`, `price`, `description`, `imageFile`, `categories` → `201 Created`, body `id` (UUID, non-empty); that `id` exists in the local store (`mt_doc_product` unless Marten naming differs). Product fields stay identity, name, price, description, image file, categories — no new fields. Do not use `dotnet run`
- [X] T017 [US2] Green-phase contract §8 in `specs/004-catalog-postgres-compose/contracts/catalog-postgres.md`: with `lgtm` stopped, §3–§4 still succeed. With `up -d --wait lgtm` in addition, `catalogapi` uses in-stack OTLP `http://lgtm:4318` (not `http://localhost:4318`) and Grafana Loki can show Catalog records `{service_name=~".*eShop.Catalog.Api.*"}` after create-product. Do not add `depends_on: lgtm`

**Checkpoint**: User Stories 1 and 2 both work; create-product API unchanged; host Catalog not required; isolated tests still not using this Postgres

---

## Phase 5: User Story 3 - Restart, stop, and recognize an unready store (Priority: P3)

**Goal**: Ordinary restart keeps catalog products. Stop makes not-ready obvious. `POST /products` against `catalogapi` does not return 201 when the store is down. Ordinary stop MUST NOT use `down -v`.

**Independent Test**: Create a product (US2 path), restart only `catalogdb` (no `-v`), wait until Ready, same `id` still in the store. Then `compose stop catalogdb`; `pg_isready` fails; `POST http://localhost:5000/products` is not 201 (connection/store error). Isolated tests pass with compose stopped.

### Tests for User Story 3 ⚠️

- [X] T018 [US3] Against current `deploy/compose.yaml` / `deploy/compose.override.yaml`, demonstrate the store state machine in `specs/004-catalog-postgres-compose/data-model.md`: **Stopped** = published `5432` / `pg_isready` fail; **Starting** = container up, healthcheck not yet succeeding; **Ready** = `--wait catalogdb` finished and `pg_isready -U catalog -d catalog` succeeds. Also record Catalog compose states: Catalog MUST NOT be considered ready while the store is not. Record any gap in operator comments before T019

### Implementation for User Story 3

- [X] T019 [US3] Document store Starting → Ready → Stopped and Catalog Starting (wait for store Ready) → Ready in comments on `deploy/compose.yaml`: ordinary stop is `compose stop` or `down` **without** `-v`; `down -v` deletes volume `catalog-pgdata` and is not the daily stop; empty first-start database is valid; credentials remain local-development defaults; `up catalogapi` starts `catalogdb` if needed and MUST NOT appear available until the store is Ready
- [X] T020 [US3] Green-phase contract §5 in `specs/004-catalog-postgres-compose/contracts/catalog-postgres.md`: with a stored product `id` from `POST http://localhost:5000/products`, restart only `catalogdb` (no `-v`), `up -d --wait catalogdb` until Ready, same `id` still present in `mt_doc_product` (FR-009, volume `catalog-pgdata`)
- [X] T021 [US3] Green-phase contract §6 in `specs/004-catalog-postgres-compose/contracts/catalog-postgres.md`: `podman compose -f deploy/compose.yaml -f deploy/compose.override.yaml stop catalogdb` (or Docker equivalent) → `pg_isready` not ready; `POST http://localhost:5000/products` to still-running `catalogapi` does **not** return 201 (connection/store error); `up -d --wait catalogapi` restores Ready Catalog with the same in-stack connection string and no new credentials
- [X] T022 [US3] With `deploy/compose.yaml` **stopped**, run `dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj` and `dotnet test tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj` (FR-012). Change `tests/Services/eShop.Catalog.Api.Tests/Support/CatalogApiFactory.cs` only if T015 broke dummy `CatalogDb`; still no live Postgres, no Grafana, and no `catalogapi`

**Checkpoint**: All user stories are independently functional

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Prove the quickstart end-to-end and keep the diff to compose + Catalog Dockerfile + one Marten line.

- [X] T023 Run the full validation in `specs/004-catalog-postgres-compose/quickstart.md` against `deploy/compose.yaml` (store `--wait catalogdb`, Catalog `--wait catalogapi`, persist, restart keeps data, stop not-ready, optional `lgtm`, isolated tests with compose down)
- [X] T024 Confirm `git diff` has no changes under `src/BuildingBlocks/BuildBlock.Logging/`, or new Catalog GET/PUT/DELETE product routes; `deploy/` has only `compose.yaml` + `compose.override.yaml`; `catalogdb` published host port `5432` only, volume `catalog-pgdata`; `catalogapi` exists (image `eshop-catalog-api:local`, in-stack `Host=catalogdb`, host URL `http://localhost:5000`); `src/Services/eShop.Catalog.Api/Dockerfile` exists; host `appsettings.Development.json` is not the documented Catalog success path

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — already complete
- **Foundational (Phase 2)**: Depends on Setup — already complete; BLOCKS remaining user-story work
- **User Story 1 (Phase 3)**: Depends on Foundational — MVP
- **User Story 2 (Phase 4)**: Depends on Foundational; needs a Ready store (US1 Green / T007 healthcheck) so `depends_on: service_healthy` and persist checks work
- **User Story 3 (Phase 5)**: Depends on Foundational; restart/stop proof needs US1 healthcheck and US2 persist path
- **Polish (Phase 6)**: Depends on the stories you intend to ship

### User Story Dependencies

- **User Story 1 (P1)**: After Phase 2 — no Catalog.Api dependency
- **User Story 2 (P2)**: After Phase 2 — independently testable once the store is Ready; adds Dockerfile + `catalogapi`; does not change US1 store-only start
- **User Story 3 (P3)**: After Phase 2 — uses US1 `--wait` and US2 create-product to prove volume persistence; stop/not-ready is independently testable

### Within Each User Story

- Contract checks MUST be run and FAIL (or race) before the compose/Dockerfile/Marten change they justify
- `catalogdb` healthcheck (T007) before `catalogapi` `depends_on: service_healthy` (T013)
- Dockerfile (T011) before override build (T013)
- Persistence Red (T014) before AutoCreate (T015)
- Same-file edits to `deploy/compose.yaml` and `deploy/compose.override.yaml` are sequential (not [P] across those tasks)
- Do not call `ApplyAllDatabaseChangesOnStartup()` (breaks `CatalogApiFactory`)

### Parallel Opportunities

- T002, T003, T004 (already done; different files)
- T011 (Dockerfile) can start after T010 in parallel with T012 (`compose.yaml` image line)
- T022 can run whenever compose is stopped (does not need Grafana)
- After US1 Green, Dockerfile work (T011) can proceed while compose comments stay with one owner

---

## Parallel Example: User Story 2

```bash
# After T010 Red, different files:
Task: "T011 Add src/Services/eShop.Catalog.Api/Dockerfile"
Task: "T012 Add catalogapi image to deploy/compose.yaml"
# Then sequential same-file override + persistence:
Task: "T013 Configure catalogapi in deploy/compose.override.yaml"
Task: "T014 Red-phase POST /products against empty Postgres"
Task: "T015 AutoCreate.CreateOrUpdate in DependencyInjection.cs"
```

US1 `compose.override.yaml` healthcheck (T007) must land before T014 `depends_on`.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1–2 are already done (compose pair + store identity)
2. Complete Phase 3: Healthcheck + `--wait catalogdb` + published host connection
3. **STOP and VALIDATE**: T009 contract §1–§2
4. Demo: start catalog store without Catalog.Api and without Grafana

### Incremental Delivery

1. Setup + compose identity → shared store
2. US1 → Ready local Postgres → MVP
3. US2 → Dockerfile + catalogapi + AutoCreate + POST persists (+ optional Loki)
4. US3 → restart keeps data; stop is obvious; isolated tests still green
5. T023–T024 → quickstart + diff guard

### Parallel Team Strategy

Prefer a single owner for `deploy/compose.yaml` and `deploy/compose.override.yaml`. A second person can own `src/Services/eShop.Catalog.Api/Dockerfile` (T011) and `DependencyInjection.cs` (T015) after T010/T014 Reds.

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to spec user stories
- Documented Catalog run is `catalogapi`, not host `dotnet run`
- `catalogdb` and `lgtm` share one compose pair; Catalog.Api MUST NOT require `lgtm`
- Do not change `BuildBlock.Logging` or Catalog create-product behavior
- Local password `catalog` is a development default only (FR-013)
- Ordinary stop must not delete `catalog-pgdata`
- In-stack store host is `catalogdb`; published host is `localhost`
- Stop at any checkpoint to validate the story independently
