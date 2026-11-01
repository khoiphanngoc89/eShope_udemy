# Tasks: Hybrid Ops and App Observability

**Input**: Design documents from `/specs/006-hybrid-observability/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included. Workspace TDD and plan require failing isolated checks before production C# (Error-document shape, fail-open sink). Compose dashboards are operator-validated via quickstart (no Testcontainers). Do not edit `tests/BuildingBlocks/BuildBlock.Logging.Tests`. Do not add a second compose file or a second Grafana. Do not make `catalogapi` depend on `elasticsearch-exporter` or `lgtm`. Do not commit unless the user asks.

**Organization**: Tasks are grouped by user story so each story can be implemented and tested independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- Compose: `deploy/compose.yaml`, `deploy/compose.override.yaml`
- Prometheus: `deploy/prometheus/prometheus.yaml`
- Grafana: `deploy/grafana/provisioning/`
- Catalog: `src/Services/eShop.Catalog.Api/`
- Catalog tests: `tests/Services/eShop.Catalog.Api.Tests/`
- Logging tests (do not modify): `tests/BuildingBlocks/BuildBlock.Logging.Tests/`
- Contracts: `specs/006-hybrid-observability/contracts/`
- Validation: `specs/006-hybrid-observability/quickstart.md`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Folders for exporter scrape config and Grafana provisioning. No new csproj.

- [X] T001 Create `deploy/prometheus/` and `deploy/grafana/provisioning/datasources/`, `deploy/grafana/provisioning/dashboards/`, `deploy/grafana/provisioning/alerting/` per `specs/006-hybrid-observability/plan.md`
- [X] T002 [P] Add a `.gitkeep` in each empty provisioning folder created in T001 so the tree is committed before dashboard JSON exists

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Elasticsearch exporter in the existing compose pair. Catalog must not wait on it. MUST complete before US1 dashboards.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T003 Add service `elasticsearch-exporter` to `deploy/compose.yaml` using image `quay.io/prometheuscommunity/elasticsearch-exporter` (pin a 1.8.x or current stable tag); no second compose file (FR-009, contracts/compose-hybrid.md)
- [X] T004 Configure `elasticsearch-exporter` in `deploy/compose.override.yaml`: `container_name` `eshop_elasticsearch_exporter`, command `--es.uri=http://elasticsearch:9200`, publish `9114:9114`, `depends_on` Elasticsearch healthy only; do **not** add `catalogapi.depends_on` exporter or `lgtm` (FR-007, FR-012, local-dev no auth)
- [X] T005 Copy the `lgtm` image default Prometheus config, then write `deploy/prometheus/prometheus.yaml` that **keeps existing LGTM scrape jobs** and adds job `elasticsearch` targeting `elasticsearch-exporter:9114` /metrics (research.md §2; do not replace with a scrape-only file)
- [X] T006 Mount `deploy/prometheus/prometheus.yaml` on `lgtm` as `/otel-lgtm/prometheus.yaml` in `deploy/compose.override.yaml` (read-only)

**Checkpoint**: `docker compose ... up -d --wait elasticsearch elasticsearch-exporter` then `curl http://localhost:9114/metrics` shows `elasticsearch_cluster_health_` when ES is up. `catalogapi` still starts with exporter/lgtm stopped.

---

## Phase 3: User Story 1 - See if the system is broken (Ops/Infra) (Priority: P1) 🎯 MVP

**Goal**: Grafana Ops/Infra (Prometheus only) shows cluster health, CPU, heap, shards, and document count. Local alerts fire on red cluster, heap > 90% for 2m, CPU > 90% for 5m. Zero documents and single-node yellow replicas must not alert.

**Independent Test**: Start `elasticsearch`, `elasticsearch-exporter`, `lgtm`. Open Grafana http://localhost:3000 (admin/admin). Ops/Infra is present without adding a Prometheus data source. Health/CPU/heap/shards/docs visible after ~1 minute. Empty index does not show cluster-down.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T007 [P] [US1] Isolated PromQL/alert-rule fixture test in `tests/Services/eShop.Catalog.Api.Tests/HybridObservabilityRulesTests.cs` that loads `deploy/grafana/provisioning/alerting/` (or the file T011 will add) and asserts: fire on cluster **red**; heap used/max **> 0.90** for **2 minutes**; process CPU **> 90%** for **5 minutes**; do **not** fire on document count `0`; do **not** fire on yellow unassigned replicas only (data-model.md SearchIndexClusterSample rules; contracts/ops-infra.md §3). Test MUST fail until T011 exists.

### Implementation for User Story 1

- [X] T008 [P] [US1] Add Grafana dashboard JSON `deploy/grafana/provisioning/dashboards/ops-infra.json` titled Ops/Infra, Prometheus data source only; panels for `elasticsearch_cluster_health_status`, `elasticsearch_process_cpu_percent`, `elasticsearch_jvm_memory_used_bytes{area="heap"}` / `elasticsearch_jvm_memory_max_bytes{area="heap"}`, `elasticsearch_cluster_health_unassigned_shards`, `elasticsearch_cluster_health_number_of_documents` (display only) (FR-002, contracts/ops-infra.md §2)
- [X] T009 [P] [US1] Add dashboard provider YAML `deploy/grafana/provisioning/dashboards/dashboards.yaml` pointing at the dashboards folder; mount provisioning on `lgtm` in `deploy/compose.override.yaml` without wiping Loki/Tempo data sources (research.md §4)
- [X] T010 [US1] Document on the Ops/Infra dashboard that zero documents is valid (empty catalog) and must not be treated as a cluster outage (FR-003)
- [X] T011 [US1] Add Grafana-managed local alert rules in `deploy/grafana/provisioning/alerting/es-cluster.yaml`: cluster **red**; heap used/max **> 0.90** for **2 minutes**; process CPU **> 90%** for **5 minutes**; no Slack/email; no alert on document count **0**; no alert on yellow replica unassigned (FR-003, SC-006)
- [X] T012 [US1] Wire alerting provision path on `lgtm` in `deploy/compose.override.yaml` so T011 loads without a second Grafana

**Checkpoint**: US1 operator path in `specs/006-hybrid-observability/quickstart.md` steps 1–3 works. Catalog writes still work with `lgtm` stopped.

---

## Phase 4: User Story 2 - See why it broke (Log/App) (Priority: P2)

**Goal**: Grafana Log/App uses a provisioned Elasticsearch data source (`catalog-app-logs*`, `@timestamp`). Catalog Error/Fatal logs (failed-request name, duration, stack) appear there. Loki Explore stays.

**Independent Test**: With compose ES + Catalog + Grafana up, cause one failed Catalog request. Log/App shows that error text within 2 minutes, filtered to `service.name` = `eShop.Catalog.Api`. Isolated tests do not start ES.

### Tests for User Story 2 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T013 [P] [US2] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/CatalogErrorDocumentTests.cs` that a Catalog Error payload includes required fields `@timestamp` (date-time, UTC), `message` (string, request name + duration + stack), `level` (`Error` or `Fatal`), `service.name` (`eShop.Catalog.Api`); `exception` optional; `trace_id` / `span_id` optional when a request activity exists (data-model.md CatalogErrorDocument). MUST fail until T015/T016 exist.
- [X] T014 [P] [US2] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/CatalogErrorDocumentTests.cs` that Information/Debug events are **not** mapped to `catalog-app-logs` (Error/Fatal only) (research.md §5)

### Implementation for User Story 2

- [X] T015 [US2] Add package `Elastic.Serilog.Sinks` (ES 8) to `src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj`; do not add a new project; do not edit `BuildBlock.Logging.Tests`
- [X] T016 [US2] Enable the Elasticsearch Serilog sink only when compose/config sets it (index `catalog-app-logs`, minimum level **Error**, time field `@timestamp`, `service.name` enricher already present) in `src/Services/eShop.Catalog.Api/appsettings.json` and/or `deploy/compose.override.yaml` Catalog env; isolated test host MUST omit the sink (FR-006, FR-008)
- [X] T017 [US2] Ensure the ES sink is fail-open: Catalog process starts if Elasticsearch is down; console + existing OTLP/Loki WriteTo stay (FR-007). Do not `ValidateOnStart` the sink.
- [X] T018 [P] [US2] Add Grafana Elasticsearch data source `deploy/grafana/provisioning/datasources/elasticsearch.yaml`: uid `elasticsearch`, URL `http://elasticsearch:9200`, index `catalog-app-logs*`, time field `@timestamp`, auth none (FR-001, FR-012, contracts/log-app.md §1)
- [X] T019 [P] [US2] Add Grafana dashboard JSON `deploy/grafana/provisioning/dashboards/log-app.json` titled Log/App, Elasticsearch data source only; error table/volume filtered to `service.name` = `eShop.Catalog.Api`; empty result (no errors) is not a configuration error (FR-005)
- [X] T020 [US2] Mount T018 (and dashboard provider if not already from T009) on `lgtm` in `deploy/compose.override.yaml` without replacing the Prometheus/Loki/Tempo data sources

**Checkpoint**: Quickstart step 4 — one Catalog Error appears on Log/App within 2 minutes. Loki Explore still works (step 5).

---

## Phase 5: User Story 3 - Keep product work working when dashboards are down (Priority: P3)

**Goal**: Catalog create/import/get-by-id and isolated `dotnet test` succeed with exporter, Grafana, and the ES log sink down.

**Independent Test**: Stop `elasticsearch-exporter` and `lgtm`. `POST /products` and `GET /products/{id}` still 201/200. `dotnet test` on Catalog, BuildingBlock, and logging suites pass with those containers stopped.

### Tests for User Story 3 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T021 [P] [US3] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/HybridObservabilityFailOpenTests.cs` that the test host (no ES sink, no Grafana) still serves create and get-by-id (FR-007, FR-008, SC-005)
- [X] T022 [P] [US3] Assert in `tests/Services/eShop.Catalog.Api.Tests/HybridObservabilityFailOpenTests.cs` (or a compose-file check test) that `deploy/compose.override.yaml` `catalogapi` does not `depends_on` `elasticsearch-exporter` or `lgtm` (FR-007)

### Implementation for User Story 3

- [X] T023 [US3] Confirm `catalogapi` in `deploy/compose.override.yaml` still `depends_on` `catalogdb` only; fix if T004/T006/T012/T020 accidentally added exporter or lgtm
- [X] T024 [US3] Run `dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj`, `tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj`, and `tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj` with Grafana/exporter stopped; fix regressions without editing logging-test source unless a compile break requires a one-line package align (prefer Catalog-only changes) (FR-008, SC-004)

**Checkpoint**: Quickstart step 6 — exporter/lgtm stopped, writes and get-by-id still succeed. Isolated suites green.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Run notes and operator validation

- [X] T025 [P] Update `README.md` Run locally + operator UI table: start `elasticsearch-exporter` with the search/LGTM stack; Grafana Ops/Infra (Prometheus) vs Log/App (Elasticsearch); `curl http://localhost:9114/metrics`; Catalog does not require exporter/Grafana (FR-011)
- [X] T026 [P] Add `006-hybrid-observability` to the Specs table in `README.md`
- [X] T027 Run operator steps in `specs/006-hybrid-observability/quickstart.md` (data sources present, Ops/Infra samples, Log/App after one Error, Loki still works, fail-open writes)
- [X] T028 Do not add Slack/email, production TLS, a second compose file, or public Catalog HTTP routes

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Story 1 (P1)**: Depends on Foundational — MVP
- **User Story 2 (P2)**: Depends on Foundational; reuses Grafana mount from US1 (T009) when present
- **User Story 3 (P3)**: Depends on Foundational; verifies fail-open after US1/US2 compose edits
- **Polish**: After desired stories

### User Story Dependencies

- **US1**: After Phase 2. Independently testable with Grafana + exporter + ES (no Catalog ES sink required)
- **US2**: After Phase 2. Log/App needs Catalog Error sink + ES data source; Ops/Infra can stay as-is
- **US3**: After Phase 2. Stronger after US1/US2 so compose fail-open is proven on the real files

### Parallel Opportunities

- T001 then T002
- T007 and T008 can start together after T006 (T007 fails until T011)
- T013, T014, T018, T019 in parallel after US2 tests are red
- T025 and T026 in parallel

### Parallel Example: User Story 1

```bash
# After T006:
Task: "T007 HybridObservabilityRulesTests.cs (fails until T011)"
Task: "T008 ops-infra.json"
Task: "T009 dashboards.yaml + lgtm mount"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 + Phase 2
2. Phase 3 (Ops/Infra + alerts)
3. **STOP**: Grafana Ops/Infra + `curl :9114/metrics`
4. Then US2 (Log/App) → US3 (fail-open) → README

### Incremental Delivery

1. Exporter + scrape → metrics exist
2. Ops/Infra dashboard + alerts → “is it broken?”
3. Catalog Error sink + Log/App → “why it broke?”
4. Fail-open + README

---

## Notes

- [P] = different files, no incomplete dependency
- Quote constraints in tasks are from data-model.md / contracts — do not invent other thresholds
- Isolated tests MUST NOT start Grafana, Prometheus, or Elasticsearch
- Do not rewrite `BuildBlock.Logging.Tests`
- Do not commit unless the user asks
