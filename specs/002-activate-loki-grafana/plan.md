# Implementation Plan: Activate Loki in Grafana

**Branch**: `002-activate-loki-grafana` | **Date**: 2026-10-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-activate-loki-grafana/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Make Loki a ready, selectable log source in the existing local Grafana stack so an operator can search Catalog (and later service) logs without adding a data source or changing how services export diagnostics.

Technical approach: keep the single `grafana/otel-lgtm` service pinned at `0.38.0`. Compose healthcheck waits for the image’s `/tmp/ready` marker (Grafana + Loki up). Prove activation with Grafana HTTP contracts in the quickstart. Do not split the stack, do not publish new product ports, do not change `BuildBlock.Logging` or Catalog settings, and do not add Catalog.Api or BuildingBlocks unit/integration test projects (FR-011; Session 2026-10-27).

## Technical Context

**Language/Version**: Compose YAML (Podman or Docker Compose). No new C# for this feature.

**Primary Dependencies**: `grafana/otel-lgtm:0.38.0`; Grafana HTTP API; Loki query via Grafana datasource proxy

**Storage**: Loki inside the LGTM container (short-lived local retention; not a repo-managed database)

**Testing**: HTTP contract checks in [quickstart.md](./quickstart.md) against [contracts/grafana-loki-api.md](./contracts/grafana-loki-api.md) (`curl` + basic auth). No new test project. Catalog.Api and BuildingBlocks automated test suites are a separate feature (FR-011).

**Target Platform**: Local developer workstation (Windows/Linux) running Podman or Docker

**Project Type**: Local observability deploy (compose overlay on the existing LGTM stack)

**Performance Goals**: Viewer searchable within 2 minutes of a healthy start (SC-001); exported Catalog records visible within 2 minutes (SC-002)

**Constraints**: Do not change service logging or OTLP export; do not replace Grafana; do not add Grafana Cloud / standalone Loki / Alloy; no custom dashboards or alert rules; no new architectural layers; no Catalog.Api / BuildingBlocks test projects (FR-011)

**Scale/Scope**: One compose file, one container, Catalog as first log producer (not as a product-API test target)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` is still the unratified placeholder. Gates below use that template’s example principles plus the workspace SDD / TDD / simplicity rules.

| Gate | Result | Evidence |
|------|--------|----------|
| Library-first | N/A | No new library. Feature is deploy config for the existing Grafana stack. |
| Test-first (non-negotiable) | PASS | HTTP checks in the quickstart are the Red phase. Compose stays unchanged until those checks fail for the right reason (Loki not ready / missing). No C# production to stub. FR-011 forbids substituting Catalog.Api / BuildingBlocks test suites for those checks. |
| Integration / contract tests | PASS | Public contract is [contracts/grafana-loki-api.md](./contracts/grafana-loki-api.md). Validation hits Grafana health, datasource list, datasource health, and LogQL range query. |
| Observability | PASS | This feature *is* activating the log store operators already send diagnostics to. |
| Simplicity / abstraction ban | PASS | One service, one healthcheck, pinned image. No Alloy, no second Grafana, no datasource-YAML fork, no new C# wrapper, no new test projects. |
| CLI interface | N/A | Constitution not ratified; operators use existing `podman compose` / `docker compose`. |
| Spec-first | PASS | Spec exists at `specs/002-activate-loki-grafana/spec.md`, including Session 2026-10-27 (FR-011). |

**Post-design re-check**: PASS. Phase 1 keeps a Grafana/Loki HTTP contract, a log-record/query model, and a curl-based quickstart. That does not add a code layer, a new process, or Catalog.Api / BuildingBlocks test projects.

## Project Structure

### Documentation (this feature)

```text
specs/002-activate-loki-grafana/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── grafana-loki-api.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
deploy/observability/
└── compose.yaml          # pin image; healthcheck waits for /tmp/ready; operator comments for Explore → Loki
```

**Structure Decision**: Change only the existing observability compose file. Do not add `deploy/observability/grafana/`, a verify script, or any `src/` / `tests/` project. Catalog and `BuildBlock.Logging` are producers from `001-serilog-otel-logging` and stay out of this diff. `eShop.Catalog.Api` and BuildingBlocks unit/integration test suites are out of this feature (FR-011); specify them separately.

## Complexity Tracking

> No constitution violations to justify.
