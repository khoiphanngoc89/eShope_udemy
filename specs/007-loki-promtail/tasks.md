# Tasks: Modern Grafana Log Collection

**Input**: Design documents from `/specs/007-loki-promtail/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included. Workspace TDD and plan require failing isolated compose/config checks before wiring the collector. Loki Explore is operator-validated via quickstart (no Testcontainers). Do not edit `tests/BuildingBlocks/BuildBlock.Logging.Tests`. Do not add a second compose file or a second Grafana/Loki. Do not make `catalogapi` or `kafka` `depends_on` `log-collector`. Do not consume Kafka topics. Do not commit unless the user asks. No new Catalog HTTP route. No Catalog C# required unless a test needs a one-liner.

**Organization**: Tasks are grouped by user story so each story can be implemented and tested independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US1B, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- Compose: `deploy/compose.yaml`, `deploy/compose.override.yaml`
- Alloy: `deploy/alloy/config.alloy`
- Catalog tests: `tests/Services/eShop.Catalog.Api.Tests/`
- Logging tests (do not modify): `tests/BuildingBlocks/BuildBlock.Logging.Tests/`
- Contracts: `specs/007-loki-promtail/contracts/`
- Validation: `specs/007-loki-promtail/quickstart.md`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Folder for the Alloy config. No new csproj.

- [x] T001 Create `deploy/alloy/` per `specs/007-loki-promtail/plan.md`
- [x] T002 [P] Add a `.gitkeep` in `deploy/alloy/` so the tree exists before `config.alloy`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: One `log-collector` in the existing compose pair. Catalog and Kafka must not wait on it. MUST complete before US1/US1B Explore checks.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T003 Isolated compose/config fixture test in `tests/Services/eShop.Catalog.Api.Tests/HybridLogCollectorRulesTests.cs` that loads `deploy/compose.override.yaml` and the file T006 will add (`deploy/alloy/config.alloy`) and asserts: `catalogapi.depends_on` is `catalogdb` only (no `log-collector` / `lgtm`); `kafka` does not `depends_on` `log-collector`; keep-list includes `eshop_catalog_api` and `eshop_kafka`; keep-list does **not** include Connect, Kafka UI, or databases; config does **not** subscribe to Kafka topics; Loki write URL is `http://lgtm:3100/loki/api/v1/push` (contracts/compose-collector.md). Test MUST fail until T004–T006 exist. Isolated tests MUST NOT start Docker.

- [x] T004 Add service `log-collector` to `deploy/compose.yaml` using image `grafana/alloy` (pin a published v1.x tag); no second compose file (FR-007, contracts/compose-collector.md)

- [x] T005 Configure `log-collector` in `deploy/compose.override.yaml`: `container_name` `eshop_log_collector`; mount `deploy/alloy/config.alloy` and `/var/run/docker.sock` read-only; `depends_on` `lgtm` healthy only; do **not** add `catalogapi.depends_on` or `kafka.depends_on` `log-collector` (FR-005, FR-010)

- [x] T006 Write `deploy/alloy/config.alloy`: Docker discovery; keep only `eshop_catalog_api` and `eshop_kafka`; label `compose_service` `catalogapi` / `kafka`; `loki.write` to `http://lgtm:3100/loki/api/v1/push`; no topic consumer; no Kafka data-dir tail (research.md §1–§3, data-model.md)

**Checkpoint**: `docker compose ... up -d --wait lgtm log-collector`. Collector starts. `catalogapi` and `kafka` still start with collector stopped.

---

## Phase 3: User Story 1 - See Catalog logs in Loki via collector (Priority: P1) 🎯 MVP

**Goal**: After one Catalog request, Explore → Loki `{compose_service="catalogapi"}` shows Catalog stdout. No new Loki data source.

**Independent Test**: Start `lgtm catalogapi log-collector`. Hit Catalog once. Grafana Explore → Loki, last 15 minutes, `{compose_service="catalogapi"}` has lines. Empty before traffic is not a config error.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T007 [P] [US1] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/HybridLogCollectorRulesTests.cs` that `deploy/alloy/config.alloy` keeps `eshop_catalog_api` and documents `compose_service` for Catalog (`catalogapi`) (data-model.md CatalogProcessLogLine; contracts/loki-explore.md §2). MUST fail until T006 exists.

### Implementation for User Story 1

- [x] T008 [US1] Confirm T006 keep-list includes `eshop_catalog_api`; fix `deploy/alloy/config.alloy` if Catalog was omitted
- [x] T009 [US1] Document Catalog collector LogQL `{compose_service="catalogapi"}` and the existing OTLP selector `{service_name=~".*eShop.Catalog.Api.*"}` in `README.md` (FR-009, contracts/loki-explore.md §2)

**Checkpoint**: Quickstart steps for Catalog lines work. Do not add a Loki data source.

---

## Phase 4: User Story 1b - See Kafka broker process logs in Loki (Priority: P1)

**Goal**: After Kafka is healthy, Explore → Loki `{compose_service="kafka"}` shows broker **process** lines, not topic payloads. Filter does not show Catalog.

**Independent Test**: Start `lgtm kafka log-collector` (Catalog not required). Last 15 minutes `{compose_service="kafka"}` has broker process lines. Kafka UI still shows topic contents.

### Tests for User Story 1b ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T010 [P] [US1B] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/HybridLogCollectorRulesTests.cs` that `deploy/alloy/config.alloy` keeps `eshop_kafka`, sets `compose_service` `kafka`, and does **not** mention Connect, Kafka UI, topic subscribe, or `eshop.catalog.sync` consume (data-model.md KafkaBrokerProcessLogLine; contracts/loki-explore.md §3). MUST fail until T006 exists.

### Implementation for User Story 1b

- [x] T011 [US1B] Confirm T006 keep-list includes `eshop_kafka` only as the Kafka target; fix `deploy/alloy/config.alloy` if broker was omitted or extra Kafka-related containers were kept
- [x] T012 [US1B] Document Kafka process LogQL `{compose_service="kafka"}` and that topic payloads stay in Kafka UI http://localhost:8080 in `README.md` (FR-004, FR-009)

**Checkpoint**: Quickstart Kafka steps work. `{compose_service="kafka"}` does not return Catalog lines.

---

## Phase 5: User Story 2 - Find why a request failed in Loki (Priority: P2)

**Goal**: After one Catalog Error (name, duration, stack), Loki filter `{compose_service="catalogapi"} |= "Error"` (or `|= "failed"`) shows that text. Elasticsearch Log/App is not required for this story.

**Independent Test**: Cause one Catalog failure. Query Loki for Catalog errors within 2 minutes. Isolated tests do not start Loki.

### Tests for User Story 2 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T013 [P] [US2] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/HybridLogCollectorRulesTests.cs` (or `README.md` fixture check) that operator notes include `{compose_service="catalogapi"}` plus an `Error` / `failed` line filter (contracts/loki-explore.md §4). MUST fail until T014 exists.

### Implementation for User Story 2

- [x] T014 [US2] Add the Catalog error LogQL to `README.md` (`|= "Error"` and/or `|= "failed"`). Do not remove Elasticsearch Log/App or OTLP. Do not add a Serilog Loki sink (FR-003, FR-008)

**Checkpoint**: Quickstart step for one Catalog Error in Loki works. Loki Explore still works for Information lines when unfiltered.

---

## Phase 6: User Story 3 - Keep product work working when the collector is down (Priority: P3)

**Goal**: Create / get-by-id and isolated `dotnet test` succeed with `log-collector` and Grafana stopped.

**Independent Test**: Stop `log-collector` (and optionally `lgtm`). `POST /products` and `GET /products/{id}` still 201/200. Isolated suites pass.

### Tests for User Story 3 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T015 [P] [US3] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/HybridLogCollectorFailOpenTests.cs` that the test host (no collector) still serves create and get-by-id (FR-005, FR-006, SC-004)
- [x] T016 [P] [US3] Assert in `tests/Services/eShop.Catalog.Api.Tests/HybridLogCollectorFailOpenTests.cs` that `deploy/compose.override.yaml` `catalogapi` and `kafka` do not `depends_on` `log-collector` (FR-005, contracts/compose-collector.md)

### Implementation for User Story 3

- [x] T017 [US3] Confirm `catalogapi` in `deploy/compose.override.yaml` still `depends_on` `catalogdb` only and `kafka` does not wait on `log-collector`; fix if T005 accidentally added it
- [x] T018 [US3] Run `dotnet test` on `tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj`, `tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj`, and `tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj` with collector/Grafana stopped; fix regressions without editing logging-test source (FR-006, SC-003)

**Checkpoint**: Quickstart fail-open step works. Isolated suites green.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Run notes and operator validation

- [x] T019 [P] Update `README.md` Run locally: start `lgtm kafka catalogapi log-collector`; Catalog/Kafka do not require the collector (FR-009)
- [x] T020 [P] Add `007-loki-promtail` to the Specs table in `README.md`
- [x] T021 Run operator steps in `specs/007-loki-promtail/quickstart.md` (Loki listed, Catalog lines, Kafka process lines, error filter, Kafka UI still topics, 006 dashboards stay, fail-open writes)
- [x] T022 Do not add Slack/email, production TLS, a second compose file, a second Grafana/Loki, topic consume, Connect/Kafka-UI collection, or public Catalog HTTP routes

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Story 1 (P1)**: Depends on Foundational — MVP (Catalog in Loki)
- **User Story 1b (P1)**: Depends on Foundational; same Alloy file as US1 (T006)
- **User Story 2 (P2)**: Depends on Foundational; stronger after US1 README LogQL
- **User Story 3 (P3)**: Depends on Foundational; verifies fail-open after compose edits
- **Polish**: After desired stories

### User Story Dependencies

- **US1**: After Phase 2. Independently testable with Catalog + collector + Grafana (Kafka not required)
- **US1B**: After Phase 2. Independently testable with Kafka + collector + Grafana (Catalog not required)
- **US2**: After Phase 2. Needs Catalog Error on stdout (already there) + collector from Phase 2
- **US3**: After Phase 2. Stronger after T005 so compose fail-open is proven on the real files

### Parallel Opportunities

- T001 then T002
- T007 and T010 can start together after T006 (both fail until T006)
- T009 and T012 can run together (same README — sequential if they collide)
- T015 and T016 in parallel
- T019 and T020 in parallel

### Parallel Example: After T006

```bash
Task: "T007 HybridLogCollectorRulesTests Catalog keep-list"
Task: "T010 HybridLogCollectorRulesTests Kafka keep-list / no topic consume"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 + Phase 2
2. Phase 3 (Catalog in Loki)
3. **STOP**: Explore `{compose_service="catalogapi"}`
4. Then US1B (Kafka process) → US2 (errors) → US3 (fail-open) → README

### Incremental Delivery

1. Collector + Alloy keep-list → scrape exists
2. Catalog LogQL → MVP
3. Kafka process LogQL → clarify 2026-11-01
4. Error filter + fail-open + README

---

## Notes

- [P] = different files, no incomplete dependency
- Quote constraints in tasks are from data-model.md / contracts — do not invent other labels
- Isolated tests MUST NOT start Grafana, Alloy, or Kafka
- Do not rewrite `BuildBlock.Logging.Tests`
- Do not commit unless the user asks
