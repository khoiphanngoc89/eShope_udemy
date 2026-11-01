# Implementation Plan: All Container Logs Without App Duplicates

**Branch**: `008-all-container-logs` | **Date**: 2026-11-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008-all-container-logs/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Give operators (1) process logs for every local-stack container that does **not** already export application diagnostics, in existing Grafana Loki, without listing Catalog request lines twice, and (2) **one Ops/Infra overview** of the product stack as healthy or not (process up), then click a container for its **detail metrics** in the same Grafana.

Technical approach: rewrite Alloy as a drop-list (`catalogapi`, `log-collector`, `lgtm`); add `cadvisor` + Prometheus job `containers` for process-up; add `postgres-exporter` + job `catalogdb` for store detail; evolve `ops-infra.json` (uid `eshop-ops-infra`) into overview + `$service` drill-down. No new .csproj. No new Catalog HTTP route. Catalog does not wait on collector, cadvisor, exporter, or Grafana.

## Technical Context

**Language/Version**: C# / .NET 10.0 (existing Catalog tests only; no new production C# expected)

**Primary Dependencies**: Existing `grafana/alloy:v1.21.3`. New `gcr.io/cadvisor/cadvisor` (v0.49+ pin at implement). New `quay.io/prometheuscommunity/postgres-exporter` (v0.17+ pin at implement). Existing `grafana/otel-lgtm:0.38.0`. Existing Catalog Serilog OTLP. Test: xUnit (existing Catalog suite)

**Storage**: Process logs in Loki inside LGTM. Process-up and store/ES/Kafka/Catalog samples in Prometheus inside LGTM. No new Catalog store. Kafka topic data unchanged.

**Testing**: Isolated compose/config checks (Alloy drop-list, Prometheus jobs, Ops/Infra inventory / `service` variable / data links, fail-open `depends_on`) + existing suites green without collector/cadvisor/exporter/Grafana. Compose quickstart proves Loki queries, no Catalog duplicate, overview + click-through, and fail-open writes.

**Target Platform**: Local Docker Desktop compose + CI `dotnet test`. No production TLS/auth.

**Project Type**: Existing compose pair + existing Catalog API (no new project)

**Performance Goals**: Isolated suites unchanged (under 2 minutes). Non-app process lines in Explore within 2 minutes (SC-001). Catalog application lines once within 2 minutes (SC-002). Overview healthy/not + one detail click within 2 minutes (SC-007). Store detail after one minute (SC-006).

**Constraints**: One Grafana. One Ops/Infra uid. One compose pair. Catalog/Kafka start without collector/cadvisor/exporter/LGTM. Do not ship Catalog container stdout. Do not ship topic payloads. Do not expose SQL text. Do not treat empty catalog size as not-healthy. Do not require collector or Grafana on the overview. Do not rewrite `BuildBlock.Logging.Tests`.

**Scale/Scope**: One Alloy config change, two exporter/sidecar services (cadvisor, postgres-exporter), two Prometheus jobs, one evolved dashboard, README. No new HTTP route.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` is still the unratified placeholder. Gates use that template’s example principles plus workspace SDD / TDD / simplicity rules.

| Gate | Result | Evidence |
|------|--------|----------|
| Library-first | PASS | No new library. Reuse Alloy + LGTM. cadvisor and postgres-exporter are compose sidecars. |
| Test-first (non-negotiable) | PASS | Red: isolated compose/config checks before production compose/Alloy/dashboard edits. No production C# before a failing test if C# is added. |
| Integration / contract tests | PASS | [contracts/loki-explore.md](./contracts/loki-explore.md), [compose-collector.md](./contracts/compose-collector.md), [ops-infra-overview.md](./contracts/ops-infra-overview.md). Isolated `dotnet test` + compose operator path. |
| Observability | PASS | Logs + stack-health overview. Fail-open Catalog. Isolated checks run with collector/cadvisor/exporter/Grafana down. 002/006 views kept. |
| Simplicity / abstraction ban | PASS | Expand existing collector; two sidecars; one dashboard uid; no new microservice; no Catalog scrape. |
| CLI interface | N/A | Operators run documented `docker compose` and Grafana. |
| Spec-first | PASS | Spec at `specs/008-all-container-logs/spec.md` including 2026-11-01 clarifications. |

**Post-design re-check**: PASS. Phase 1 adds process-line / application-line / health-status / detail models, operator contracts, and a `dotnet test` + compose quickstart. Catalog still starts if collector/cadvisor/exporter/Grafana are down. Catalog application lines have one path. Overview healthy is process-up. Store size 0 is not not-healthy. SQL text is not scraped. Logging suite is not rewritten.

## Project Structure

### Documentation (this feature)

```text
specs/008-all-container-logs/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── compose-collector.md
│   ├── loki-explore.md
│   ├── ops-infra-overview.md
│   └── ops-infra-store.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
deploy/
├── compose.yaml                              # add postgres-exporter + cadvisor
├── compose.override.yaml                     # exporter/cadvisor env/health; collector mounts stay
├── alloy/config.alloy                        # drop-list + compose_service from metadata
├── prometheus/prometheus.yaml                # + catalogdb + containers jobs
└── grafana/provisioning/dashboards/ops-infra.json  # overview + $service detail

tests/Services/eShop.Catalog.Api.Tests/       # rewrite 007 keep-list; inventory / fail-open checks
README.md                                     # LogQL; Catalog = OTLP once; overview + click
```

**Structure Decision**: Extend the existing compose pair only. Do not add a project. Do not put collector or exporter config inside Catalog. Do not add a second compose file or a second dashboard uid as the operator home.

## Complexity Tracking

> No constitution violations that require justification.
