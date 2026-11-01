# Tasks: All Container Logs Without App Duplicates

**Input**: Design documents from `/specs/008-all-container-logs/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included. Workspace TDD and plan require failing isolated compose/config checks before Alloy, cadvisor, exporter, Prometheus, or Ops/Infra edits. Explore and Ops/Infra are operator-validated via `specs/008-all-container-logs/quickstart.md` (no Testcontainers). Do not edit `tests/BuildingBlocks/BuildBlock.Logging.Tests`. Do not add a second compose file, a second Grafana, or a second dashboard uid as the operator home. Do not make `catalogapi` or `kafka` `depends_on` `log-collector`, `lgtm`, `postgres-exporter`, or `cadvisor`. Do not consume Kafka topics. Do not enable `stat_statements`. Do not commit unless the user asks. No new Catalog HTTP route. No new production C# unless a test needs a one-liner.

**Organization**: Tasks are grouped by user story so each story can be implemented and tested independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US1B, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- Compose: `deploy/compose.yaml`, `deploy/compose.override.yaml`
- Alloy: `deploy/alloy/config.alloy`
- Prometheus: `deploy/prometheus/prometheus.yaml`
- Ops/Infra: `deploy/grafana/provisioning/dashboards/ops-infra.json`
- Catalog tests: `tests/Services/eShop.Catalog.Api.Tests/`
- Logging tests (do not modify): `tests/BuildingBlocks/BuildBlock.Logging.Tests/`
- Contracts: `specs/008-all-container-logs/contracts/`
- Validation: `specs/008-all-container-logs/quickstart.md`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm existing deploy trees. No new csproj. No second compose file.

- [X] T001 Confirm `deploy/alloy/`, `deploy/prometheus/`, and `deploy/grafana/provisioning/dashboards/` exist per `specs/008-all-container-logs/plan.md`; do not add a new `.csproj` or a second compose file

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Rewrite Alloy from the 007 two-name keep-list to default-collect network `eshop` with an application drop-list. Shared by US1 (coverage) and US2 (no Catalog container copy). MUST complete before story Explore checks.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T002 Rewrite isolated Alloy assertions in `tests/Services/eShop.Catalog.Api.Tests/HybridLogCollectorRulesTests.cs`: drop-list includes `catalogapi`, `log-collector`, and `lgtm`; keep filter uses network `eshop` (one target per container); `compose_service` comes from `__meta_docker_container_label_com_docker_compose_service`; Loki write URL is `http://lgtm:3100/loki/api/v1/push`; config does **not** `subscribe` / `consumer` / consume `eshop.catalog.sync` or tail `/var/lib/kafka`; invert 007 `DoesNotContain` of `catalogdb` / `elasticsearch` / `kafka-ui` / `eshop_debezium`. Isolated tests MUST NOT start Docker. Test MUST fail until T004 exists (contracts/compose-collector.md; data-model.md ContainerProcessLine: `compose_service` required; line body is process text, **Not** a Kafka topic message body, **Not** a copy of Catalog application diagnostics)

- [X] T003 Confirm `tests/Services/eShop.Catalog.Api.Tests/HybridLogCollectorFailOpenTests.cs` and `tests/Services/eShop.Catalog.Api.Tests/HybridObservabilityFailOpenTests.cs` still assert `catalogapi.depends_on` is `catalogdb` only (no `log-collector` / `lgtm`) in `deploy/compose.override.yaml` (FR-005)

- [X] T004 Rewrite `deploy/alloy/config.alloy`: Docker discovery; keep containers on network `eshop` and collapse to one target per container; drop `catalogapi`, `log-collector`, and `lgtm`; set `compose_service` from compose metadata; optional `container` from `__meta_docker_container_name`; `loki.write` to `http://lgtm:3100/loki/api/v1/push`; no topic consumer; no Kafka data-dir tail (research.md §1; data-model.md LogCollector: Drop `catalogapi`, `log-collector`, `lgtm`)

**Checkpoint**: Isolated T002 is green. `catalogapi` still starts with `log-collector` stopped. Do not start Grafana for this checkpoint.

---

## Phase 3: User Story 1 - See every stack container’s process logs in Grafana (Priority: P1) 🎯 MVP

**Goal**: After each non-application stack container is healthy, Explore → Loki `{compose_service="<name>"}` shows that container’s process lines. No new Loki data source. Kafka topic bodies stay in Kafka UI.

**Independent Test**: Start the documented stack and log viewer. After each target is healthy, search the last 15 minutes by identity. Process lines appear without adding a data source. Empty before first write is not a config error. Kafka query is broker process text, not topic JSON.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T005 [US1] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/HybridLogCollectorRulesTests.cs` that `deploy/alloy/config.alloy` maps `compose_service` from compose metadata so `catalogdb`, `kafka`, `connect`, `elasticsearch`, `kibana`, and `kafka-ui` can be labeled (not a two-name keep-list) (data-model.md ContainerProcessLine: `compose_service` required; contracts/loki-explore.md §2). MUST fail until T004 metadata mapping exists

- [X] T006 [US1] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/HybridLogCollectorRulesTests.cs` that `README.md` includes `{compose_service="catalogdb"}`, `{compose_service="kafka"}`, `{compose_service="connect"}`, `{compose_service="elasticsearch"}`, `{compose_service="kibana"}`, `{compose_service="kafka-ui"}` and that Kafka UI `http://localhost:8080` remains the topic viewer (contracts/loki-explore.md §2–§4; FR-009). MUST fail until T007 exists

### Implementation for User Story 1

- [X] T007 [US1] Document all-container LogQL in `README.md`: last 15 minutes `{compose_service="<name>"}` for store, broker, connector, search index, operator UIs, and exporters when started; empty result is valid “no logs yet”; `{compose_service="kafka"}` is broker process text, not topic JSON; topic payloads stay in Kafka UI `http://localhost:8080` (FR-002, FR-004, FR-009, SC-005)

**Checkpoint**: Quickstart Loki steps for non-app identities work. Do not add a Loki data source.

---

## Phase 4: User Story 1b - See every container healthy or not, then open detail metrics (Priority: P1)

**Goal**: Ops/Infra is one overview of the product stack as healthy (process up) or not (stopped or missing). Click an identity to open that container’s detail on the same dashboard (`service` variable). Store detail is up / connections / size. Empty size is not not-healthy. Collector and Grafana are not required on the board.

**Independent Test**: Start store + product stack + Grafana + cadvisor. Open Ops/Infra. Overview lists the product-stack identities. Click catalogdb for store detail; click another identity for its detail. Create/get-by-id still work if cadvisor/exporter are stopped. A product-stack container that was not started shows as not healthy.

### Tests for User Story 1b ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T008 [US1B] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/HybridObservabilityRulesTests.cs` that `deploy/compose.yaml` / `deploy/compose.override.yaml` declare `cadvisor` (`gcr.io/cadvisor/cadvisor` or `ghcr.io/google/cadvisor`, `container_name` `eshop_cadvisor`) with Docker socket read-only, and `postgres-exporter` (`quay.io/prometheuscommunity/postgres-exporter`, `container_name` `eshop_postgres_exporter`, `DATA_SOURCE_NAME` to `catalogdb` / database `catalog`); `catalogapi.depends_on` does **not** include `cadvisor`, `postgres-exporter`, or `lgtm` (FR-013; contracts/compose-collector.md). Isolated tests MUST NOT start Docker. MUST fail until T012–T013 exist

- [X] T009 [US1B] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/HybridObservabilityRulesTests.cs` that `deploy/prometheus/prometheus.yaml` keeps existing `elasticsearch` / `kafka` jobs and OTLP/self settings and adds `job_name: catalogdb` → `postgres-exporter:9187` and `job_name: containers` → `cadvisor:8080`; files do **not** enable `collector.stat_statements` or `include_query` (FR-014; data-model.md StoreHealthSample: SQL text / per-query payload **Forbidden**). MUST fail until T014 exists

- [X] T010 [US1B] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/HybridObservabilityRulesTests.cs` that `deploy/grafana/provisioning/dashboards/ops-infra.json` keeps uid `eshop-ops-infra` and title `Ops/Infra`, lists overview identities `catalogdb`, `catalogapi`, `kafka`, `connect`, `elasticsearch`, `kibana`, `kafka-ui`, `elasticsearch-exporter`, `kafka-exporter`, `postgres-exporter`, has template variable `service`, and uses a data link containing `var-service`; MUST NOT require `log-collector` or `lgtm` as overview identities (data-model.md ContainerHealthStatus: Healthy = process is up; Stopped or missing = not healthy; Empty data is not not-healthy; contracts/ops-infra-overview.md §1–§2). MUST fail until T015 exists

- [X] T011 [US1B] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/HybridObservabilityRulesTests.cs` that `ops-infra.json` store detail includes `pg_up` (or `up{job="catalogdb"}`), `pg_stat_database_numbackends{datname="catalog"}`, and `pg_database_size_bytes{datname="catalog"}`; dashboard text must not treat zero/small size as not-healthy or an outage; no `stat_statements` / SQL statement text; ES / Kafka / Catalog.Api richer panels remain (data-model.md ContainerDetailMetrics and StoreHealthSample; contracts/ops-infra-overview.md §3). MUST fail until T015 exists

### Implementation for User Story 1b

- [X] T012 [US1B] Add services `cadvisor` and `postgres-exporter` to `deploy/compose.yaml` (`gcr.io/cadvisor/cadvisor` or `ghcr.io/google/cadvisor` pin v0.49+; `quay.io/prometheuscommunity/postgres-exporter` pin v0.17+); no second compose file (FR-007)

- [X] T013 [US1B] Configure both sidecars in `deploy/compose.override.yaml`: `cadvisor` `container_name` `eshop_cadvisor`, Docker socket read-only, scrape port `8080`; `postgres-exporter` `container_name` `eshop_postgres_exporter`, `DATA_SOURCE_NAME=postgresql://catalog:catalog@catalogdb:5432/catalog?sslmode=disable`, `depends_on` `catalogdb` healthy only, do **not** pass `--collector.stat_statements`; do **not** add `catalogapi.depends_on` `cadvisor` / `postgres-exporter` / `lgtm` (FR-010, FR-013, FR-014)

- [X] T014 [US1B] Append scrape jobs in `deploy/prometheus/prometheus.yaml`: `catalogdb` → `postgres-exporter:9187` and `containers` → `cadvisor:8080`, without dropping LGTM OTLP/self settings or existing `elasticsearch` / `kafka` jobs; relabel compose metadata to `compose_service` on the containers job when the scrape provides it (research.md §3–§4)

- [X] T015 [US1B] Evolve `deploy/grafana/provisioning/dashboards/ops-infra.json` (uid `eshop-ops-infra`): top overview of the fixed product-stack list as healthy/not (process up vs stopped or missing); data links set `var-service`; every identity’s detail includes CPU and RAM; `catalogdb` also shows `pg_up`, `pg_stat_database_numbackends{datname="catalog"}`, `pg_database_size_bytes{datname="catalog"}`; `catalogapi` / `kafka` / `elasticsearch` keep existing richer panels; state that small or zero size is not not-healthy; do not list `log-collector` or `lgtm` as required identities (FR-008, FR-012, SC-006, SC-007)

- [X] T016 [US1B] Document the Ops/Infra overview and click-for-detail path in `README.md`: product-stack inventory; healthy = process up; stopped or missing = not healthy; empty catalog is not an outage; how to click `catalogdb` for store detail; Catalog does not scrape the store or cadvisor (FR-009)

**Checkpoint**: Quickstart Ops/Infra overview + click steps work. Explore logs still work. No new Prometheus data source. No second dashboard uid as the operator home.

---

## Phase 5: User Story 2 - Catalog application logs appear once (Priority: P1)

**Goal**: After one Catalog request, Explore shows those application lines **once** from the OTLP path. Alloy does not tail `catalogapi` stdout. Error lines still include request name, duration, and stack when recorded.

**Independent Test**: Generate at least one Catalog request with the log viewer up. Search Catalog application diagnostics. The same request line is not listed twice. Isolated tests do not start the viewer.

### Tests for User Story 2 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T017 [US2] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/HybridLogCollectorRulesTests.cs` that `deploy/alloy/config.alloy` drops `catalogapi` / does **not** keep `eshop_catalog_api` as a collect target (data-model.md ApplicationDiagnosticLine: Alloy MUST NOT also tail catalogapi stdout; FR-003). MUST fail if T004 omitted the drop

- [X] T018 [US2] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/HybridLogCollectorRulesTests.cs` that `README.md` documents Catalog Explore as `{service_name=~".*eShop.Catalog.Api.*"}` (and Error / `failed` on that selector) and does **not** present `{compose_service="catalogapi"}` as the Catalog request-log path (FR-003, FR-009, SC-002; contracts/loki-explore.md §3). MUST fail until T020 exists

### Implementation for User Story 2

- [X] T019 [US2] Confirm T004 drop-list in `deploy/alloy/config.alloy` includes `catalogapi`; fix if Catalog stdout was still kept

- [X] T020 [US2] Update `README.md`: Catalog application path is OTLP `{service_name=~".*eShop.Catalog.Api.*"}` only; remove 007 wording that `{compose_service="catalogapi"}` is the Catalog request stream; keep Error usefulness (request name, duration, stack) on the OTLP selector; keep Log/App; Catalog.Api may still appear on the Ops/Infra **health** overview (FR-003, FR-008, FR-011)

**Checkpoint**: Quickstart Catalog-once steps work. `{compose_service="kafka"}` still shows broker process lines.

---

## Phase 6: User Story 3 - Keep product work working when collection is down (Priority: P2)

**Goal**: Create / get-by-id and isolated `dotnet test` succeed with `log-collector`, `cadvisor`, `postgres-exporter`, and Grafana stopped.

**Independent Test**: Leave collector and Grafana stopped. Catalog starts; create and get-by-id succeed; isolated suites pass.

### Tests for User Story 3 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T021 [P] [US3] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/HybridLogCollectorFailOpenTests.cs` that the test host (no collector, no Grafana) still serves create and get-by-id (FR-005, FR-006, SC-004)

- [X] T022 [P] [US3] Assert in `tests/Services/eShop.Catalog.Api.Tests/HybridObservabilityFailOpenTests.cs` that `deploy/compose.override.yaml` `catalogapi` does not `depends_on` `log-collector`, `lgtm`, `postgres-exporter`, or `cadvisor`, and `kafka` does not `depends_on` those services (FR-005, FR-013; contracts/compose-collector.md)

### Implementation for User Story 3

- [X] T023 [US3] Confirm `catalogapi` in `deploy/compose.override.yaml` still `depends_on` `catalogdb` only and `kafka` does not wait on `log-collector`, `lgtm`, `postgres-exporter`, or `cadvisor`; fix if T013 accidentally added a gate

- [X] T024 [US3] Run `dotnet test` on `tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj`, `tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj`, and `tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj` with collector/cadvisor/exporter/Grafana stopped; fix regressions without editing logging-test source (FR-006, SC-003)

**Checkpoint**: Quickstart fail-open step works. Isolated suites green.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Run notes and operator validation

- [X] T025 Update `README.md` Run locally: start `catalogdb postgres-exporter cadvisor lgtm` plus stack services and `log-collector`; Catalog/Kafka do not require collector, cadvisor, exporter, or Grafana (FR-009)

- [X] T026 Add `008-all-container-logs` to the Specs table in `README.md`

- [X] T027 Run operator steps in `specs/008-all-container-logs/quickstart.md` (Loki listed, non-app identities, Kafka process not topic JSON, Catalog OTLP once, Ops/Infra overview + click detail, missing container is not healthy, 006 views stay, fail-open writes)

- [X] T028 Confirm `deploy/compose.yaml`, `deploy/compose.override.yaml`, `deploy/alloy/config.alloy`, `deploy/prometheus/prometheus.yaml`, `deploy/grafana/provisioning/dashboards/ops-infra.json`, and `README.md` do not add Slack/email, production TLS, a second compose file, a second Grafana/Loki, a second dashboard uid as the operator home, topic consume, `stat_statements`, Catalog scrape of the store, or public Catalog HTTP routes

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (Alloy drop-list)
- **User Story 1 (P1)**: Depends on Foundational — MVP (all non-app process logs)
- **User Story 1b (P1)**: Depends on Foundational; own cadvisor / exporter / Prometheus / dashboard files (not Alloy)
- **User Story 2 (P1)**: Depends on Foundational; locks Catalog-once (Alloy drop + README)
- **User Story 3 (P2)**: Depends on Foundational; stronger after T013 so sidecars are not Catalog start gates
- **Polish**: After desired stories

### User Story Dependencies

- **US1**: After Phase 2. Independently testable with stack + collector + Grafana (cadvisor not required)
- **US1B**: After Phase 2. Independently testable with product stack + `cadvisor` + `postgres-exporter` + `lgtm` (Alloy drop-list not required for the board)
- **US2**: After Phase 2. Independently testable with Catalog + Grafana (collector must drop `catalogapi`)
- **US3**: After Phase 2. Stronger after US1B compose edits so `cadvisor` / `postgres-exporter` are not Catalog start gates

### Parallel Opportunities

- T005 then T006 then T007 (same test file, then `README.md`)
- T008 then T009 then T010 then T011 (same `HybridObservabilityRulesTests.cs`)
- After Phase 2, US1 (Alloy/README) and US1B (compose/prometheus/dashboard) can proceed in parallel
- T021 and T022 in parallel (different test files)
- T025 then T026 (same `README.md`)

### Parallel Example: After Phase 2

```bash
Task: "T005 HybridLogCollectorRulesTests compose_service identities"
Task: "T008 HybridObservabilityRulesTests cadvisor + postgres-exporter compose wiring"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 + Phase 2 (Alloy drop-list)
2. Phase 3 (all non-app process logs in Loki)
3. **STOP**: Explore `{compose_service="catalogdb"}` / `{compose_service="kafka"}` / other identities
4. Then US1B (overview + click) → US2 (Catalog once) → US3 (fail-open) → README

### Incremental Delivery

1. Alloy default-collect + drop-list → coverage exists without Catalog duplicate
2. README LogQL for identities → MVP
3. cadvisor + postgres-exporter + Ops/Infra overview / `$service` detail → clarify 2026-11-01
4. README Catalog = OTLP once + fail-open + specs table

---

## Notes

- [P] = different files, no incomplete dependency
- Quote constraints in tasks are from data-model.md / contracts — do not invent other labels or PromQL
- Isolated tests MUST NOT start Grafana, Alloy, cadvisor, postgres-exporter, or Kafka
- Do not rewrite `BuildBlock.Logging.Tests`
- Do not commit unless the user asks
- Future application services: add their compose service name to the Alloy drop-list (FR-011)
