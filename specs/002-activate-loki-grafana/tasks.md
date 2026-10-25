# Tasks: Activate Loki in Grafana

**Input**: Design documents from `/specs/002-activate-loki-grafana/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included. Plan, quickstart, and workspace TDD rules require Red (HTTP contract checks that fail for the right reason) then Green (compose changes). No new test project and no C# production.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- Only production file: `deploy/observability/compose.yaml`
- Contracts: `specs/002-activate-loki-grafana/contracts/grafana-loki-api.md`
- Validation: `specs/002-activate-loki-grafana/quickstart.md`
- Do not add `deploy/observability/grafana/`, a verify script, or any `src/` / `tests/` project

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the existing single-service LGTM stack is the only change surface.

- [X] T001 Record the current `lgtm` service in `deploy/observability/compose.yaml` (`image: grafana/otel-lgtm:latest`, ports `3000`, `4317`, `4318` only, no `healthcheck`) so later tasks pin the image and wait on Loki without adding host Loki `:3100`
- [X] T002 [P] Confirm Catalog OTLP export stays `http://localhost:4318` / `HttpProtobuf` in `src/Services/eShop.Catalog.Api/appsettings.json` and that `src/BuildingBlocks/BuildBlock.Logging/` is not in this feature’s diff (FR-008)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Pin a stable LGTM image so the pre-provisioned Loki datasource (`name` `Loki`, `type` `loki`, `uid` `loki`, in-container `url` `http://127.0.0.1:3100`) cannot drift with `:latest`.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T003 Replace `grafana/otel-lgtm:latest` in `deploy/observability/compose.yaml` with a published version tag chosen at implement time (keep one `lgtm` service; do not split Grafana/Loki/Alloy)

**Checkpoint**: Foundation ready — image is pinned; user story work can begin

---

## Phase 3: User Story 1 - Open Grafana and query logs through Loki immediately (Priority: P1) 🎯 MVP

**Goal**: After `compose up -d --wait`, Grafana lists Loki as an enabled log source. An operator can open Explore, select Loki, search the last 15 minutes, and get either rows or a clear empty result — with no manual data-source setup.

**Independent Test**: Start only `deploy/observability/compose.yaml` with `--wait` (no Catalog). `GET http://localhost:3000/api/datasources` includes Loki; `GET /api/datasources/uid/loki/health` is OK; empty `{service_name="eShop.Catalog.Api"}` over the last 15 minutes returns success with zero rows.

### Tests for User Story 1 ⚠️

> **NOTE: Run these checks FIRST against the current compose (no healthcheck / no `--wait`). They must fail or be racy for the right reason before Green.**

- [X] T004 [US1] Run Red-phase contract checks from `specs/002-activate-loki-grafana/contracts/grafana-loki-api.md` against `deploy/observability/compose.yaml` started with `up -d` (no `--wait`): `GET /api/health`, `GET /api/datasources`, `GET /api/datasources/uid/loki`, `GET /api/datasources/uid/loki/health` using basic `admin`/`admin`. Record that Ready (`/tmp/ready` / Loki health OK) is not guaranteed before Grafana answers on `:3000`

### Implementation for User Story 1

- [X] T005 [US1] Add a compose `healthcheck` on service `lgtm` in `deploy/observability/compose.yaml` that succeeds only when `/tmp/ready` exists (image writes that file after Loki `http://127.0.0.1:3100/ready` and Grafana `http://127.0.0.1:3000/api/health` are 200). Do not publish host port `3100`
- [X] T006 [US1] Change operator start comments in `deploy/observability/compose.yaml` to `podman compose -f deploy/observability/compose.yaml up -d --wait` (and Docker equivalent) and document Explore → select Loki (`name` MUST be `Loki`, `type` MUST be `loki`, `uid` MUST be `loki`) with no manual data-source create
- [X] T007 [US1] Green-phase: `up -d --wait` then pass contract §1–§4 in `specs/002-activate-loki-grafana/contracts/grafana-loki-api.md`: health 200; datasource list includes `name=Loki`, `type=loki`, `uid=loki`; `GET /api/datasources/uid/loki/health` status OK; last-15-minute `{service_name="eShop.Catalog.Api"}` with Catalog off returns HTTP 200, success, **zero rows** (empty list is success, not a connection error)

**Checkpoint**: User Story 1 is independently testable (Loki selectable and queryable after `--wait`, no Catalog required)

---

## Phase 4: User Story 2 - Find a service’s recent diagnostic records in Loki (Priority: P2)

**Goal**: Catalog records already exported to `http://localhost:4318` are queryable in Loki by `service_name` `eShop.Catalog.Api`, severity, time range, and optional `trace_id`. Do not change service logging.

**Independent Test**: Stack healthy + Catalog running + one Catalog route. Explore → Loki `{service_name="eShop.Catalog.Api"}` over the last 15 minutes shows those rows within 2 minutes; other services are excluded by that selector.

### Tests for User Story 2 ⚠️

- [X] T008 [US2] With the healthy stack from US1 and Catalog **not** running, confirm `{service_name="eShop.Catalog.Api"}` over the last 15 minutes still returns success with zero rows per `specs/002-activate-loki-grafana/contracts/grafana-loki-api.md` §4 (empty list is success)

### Implementation for User Story 2

- [X] T009 [US2] Add operator LogQL comments to `deploy/observability/compose.yaml`: primary selector `{service_name="eShop.Catalog.Api"}` (`service_name` is required; Catalog value `eShop.Catalog.Api`); default validation window last 15 minutes; severity via `detected_level` or `level` confirmed on the pinned image; request-time rows may show `trace_id`. Do not change `src/Services/eShop.Catalog.Api/appsettings.json`
- [X] T010 [US2] Green-phase contract §5 in `specs/002-activate-loki-grafana/contracts/grafana-loki-api.md`: run `src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj`, hit an existing route (e.g. `POST /products`), then query `{service_name="eShop.Catalog.Api"}` for the last 15 minutes and confirm rows appear within 2 minutes, are isolated by `service_name`, and show `trace_id` when the producer had a request activity

**Checkpoint**: User Stories 1 and 2 both work; Catalog settings unchanged

---

## Phase 5: User Story 3 - Understand when Loki is not ready or has nothing to show (Priority: P3)

**Goal**: Compose `--wait` means Starting is not “up”. Empty filters stay an empty success. If Loki later dies, Grafana reports Unavailable — not a setup wizard. Restart restores uid `loki` with no manual setup.

**Independent Test**: `--wait` blocks until Ready. Healthy empty query → zero rows. After stop, `/api/datasources/uid/loki/health` is ERROR (Grafana `/api/health` may still be 200). `up -d --wait` again restores the same Loki uid.

### Tests for User Story 3 ⚠️

- [X] T011 [US3] Against current `deploy/observability/compose.yaml`, demonstrate the state machine in `specs/002-activate-loki-grafana/data-model.md`: **Starting** = Grafana `:3000` may answer while `/tmp/ready` is absent; **Ready** = `--wait` finished and Loki health OK; **Unavailable** = Grafana reachable and `GET /api/datasources/uid/loki/health` non-200 or `status` ERROR. Record any gap before comments/healthcheck polish

### Implementation for User Story 3

- [X] T012 [US3] Document Starting → Ready → Unavailable and “empty search is not an error” in comments on `deploy/observability/compose.yaml` (do not add a custom Grafana error page; do not add a create-datasource wizard)
- [X] T013 [US3] Green-phase: `up -d --wait` → Ready; empty `{service_name="eShop.Catalog.Api"}` last 15 minutes → 200 + zero rows; stop the `lgtm` container (or Loki inside it) → Explore / `GET /api/datasources/uid/loki/health` reports unavailable; `up -d --wait` again → same `uid` `loki` works with no manual setup (FR-007, FR-010)

**Checkpoint**: All user stories are independently functional

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Prove the quickstart end-to-end and keep the diff to compose only.

- [X] T014 Run the full validation in `specs/002-activate-loki-grafana/quickstart.md` against `deploy/observability/compose.yaml` (start `--wait`, datasource checks, empty query, Catalog query, unavailable/restart)
- [X] T015 Confirm `deploy/observability/compose.yaml` still publishes only `3000`, `4317`, and `4318`; still one `lgtm` service; `git diff` has no changes under `src/` or `tests/`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational — MVP
- **User Story 2 (Phase 4)**: Depends on Foundational; needs a queryable Loki (US1 Green) to prove Catalog rows
- **User Story 3 (Phase 5)**: Depends on Foundational; uses the US1 healthcheck; independently testable by stopping the stack
- **Polish (Phase 6)**: Depends on the stories you intend to ship

### User Story Dependencies

- **User Story 1 (P1)**: After Phase 2 — no Catalog dependency
- **User Story 2 (P2)**: After Phase 2 — independently testable once Loki answers queries; uses existing Catalog export, does not edit Catalog
- **User Story 3 (P3)**: After Phase 2 — independently testable by Ready / empty / stop / restart; comments-only if US1 healthcheck already exists

### Within Each User Story

- HTTP checks MUST be run and FAIL (or race) before the compose change they justify
- Comments/healthcheck before Green-phase re-check
- Same-file edits to `deploy/observability/compose.yaml` are sequential (not [P])

### Parallel Opportunities

- T001 and T002 (different files: compose inventory vs Catalog/logging confirmation)
- After Phase 2, US2 comments and US3 comments still collide on `compose.yaml` — do not parallelize those
- US2 Catalog run (T010) can proceed once US1 Green (T007) has a healthy Loki

---

## Parallel Example: Setup

```bash
# Different files, no dependencies:
Task: "T001 Record current lgtm service in deploy/observability/compose.yaml"
Task: "T002 Confirm Catalog appsettings.json OTLP export unchanged"
```

US1–US3 all edit `deploy/observability/compose.yaml` — run them sequentially (P1 → P2 → P3).

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Pin the image
3. Complete Phase 3: Healthcheck + `--wait` + Loki listed/queryable
4. **STOP and VALIDATE**: T007 contract §1–§4
5. Demo Explore → Loki with an empty last-15-minute search

### Incremental Delivery

1. Setup + pin → stable image
2. US1 → Loki activated without Catalog → MVP
3. US2 → Catalog rows by `service_name` (no appsettings change)
4. US3 → Starting / empty / Unavailable / restart
5. T014–T015 → quickstart + diff guard

### Parallel Team Strategy

One compose file. Prefer a single implementer after Phase 2. A second person can run T002 and later T010/T014 while compose edits stay with one owner.

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to spec user stories
- Do not add a second Grafana, Alloy, Loki service, datasource YAML mount, or host `:3100`
- Do not change `BuildBlock.Logging` or Catalog `Serilog` settings
- Auth remains Grafana `admin` / `admin`
- Stop at any checkpoint to validate the story independently
