# Implementation Plan: Modern Grafana Log Collection

**Branch**: `007-loki-promtail` | **Date**: 2026-11-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-loki-promtail/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Give operators Catalog and Kafka **broker process** logs in the existing Grafana Loki via one Grafana-family collector (Option 1). The collector tails Docker stdout for `eshop_catalog_api` and `eshop_kafka` only and pushes to Loki inside `lgtm`. Topic payloads stay in Kafka UI. Catalog and Kafka do not `depends_on` the collector. Isolated `dotnet test` does not start it. One compose pair. No new .csproj. No Catalog C# change required (console already has failed-request Errors).

Technical approach: pin `grafana/alloy`, Docker service discovery on the engine socket, keep-list two container names, Loki write `http://lgtm:3100/loki/api/v1/push` (already reachable on network `eshop`). Keep 002 OTLP and 006 hybrid views.

## Technical Context

**Language/Version**: C# / .NET 10.0 (existing Catalog tests only; no new production C# expected)

**Primary Dependencies**: Compose `grafana/alloy` (v1.x pin at implement). Existing `grafana/otel-lgtm:0.38.0` Loki. Existing Catalog Serilog console + OTLP. Test: xUnit (existing Catalog suite)

**Storage**: Process logs in Loki inside LGTM. No new Catalog store. Kafka topic data unchanged.

**Testing**: Isolated compose/config checks + existing Catalog/BuildingBlock/logging suites green without the collector. Compose quickstart proves Loki queries and fail-open writes.

**Target Platform**: Local Docker Desktop compose + CI `dotnet test`. No production TLS/auth.

**Project Type**: Existing compose pair + existing Catalog API (no new project)

**Performance Goals**: Isolated suites unchanged (under 2 minutes). Catalog and Kafka process lines visible in Explore within 2 minutes after traffic / broker healthy (SC-001, SC-002).

**Constraints**: One Grafana. One compose pair. Catalog/Kafka start without collector. Do not collect Connect/Kafka UI/DBs. Do not ship topic payloads. Do not remove OTLP, Ops/Infra, Log/App, Kibana, or Kafka UI. Do not rewrite `BuildBlock.Logging.Tests`.

**Scale/Scope**: One collector service, one Alloy config, two container targets, README LogQL. No new HTTP route.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` is still the unratified placeholder. Gates use that template’s example principles plus workspace SDD / TDD / simplicity rules.

| Gate | Result | Evidence |
|------|--------|----------|
| Library-first | PASS | No new library. Collector is a compose sidecar. Reuse Loki in LGTM. |
| Test-first (non-negotiable) | PASS | Red: isolated compose `depends_on` + keep-list file checks before wiring production compose. No production C# before a failing test if C# is added. |
| Integration / contract tests | PASS | [contracts/loki-explore.md](./contracts/loki-explore.md), [compose-collector.md](./contracts/compose-collector.md). Isolated `dotnet test` + compose operator path. |
| Observability | PASS | This feature *is* log collection. Fail-open Catalog/Kafka. Isolated checks run with collector down. 002/006 kept. |
| Simplicity / abstraction ban | PASS | One collector, two targets, no new microservice, no Serilog Loki sink, no topic consumer. |
| CLI interface | N/A | Operators run documented `docker compose` and Grafana Explore. |
| Spec-first | PASS | Spec at `specs/007-loki-promtail/spec.md`. |

**Post-design re-check**: PASS. Phase 1 adds process-log line models, two operator contracts, and a `dotnet test` + compose quickstart. Catalog still starts if the collector is down. Topic payloads are not the Loki path. Logging suite is not rewritten.

## Project Structure

### Documentation (this feature)

```text
specs/007-loki-promtail/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── loki-explore.md
│   └── compose-collector.md
├── review-notes.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
deploy/
├── compose.yaml                 # add log-collector
├── compose.override.yaml        # socket, Alloy mount, lgtm depends_on for collector only
└── alloy/config.alloy           # docker SD + keep two names + loki write

tests/Services/eShop.Catalog.Api.Tests/   # compose/config isolated checks
README.md                                  # start collector + LogQL Catalog vs Kafka
```

**Structure Decision**: Extend the existing compose pair only. Do not add a project. Do not put collector config inside Catalog. Do not add a second compose file.

## Complexity Tracking

> No constitution violations that require justification.
