# Implementation Plan: Hybrid Ops and App Observability

**Branch**: `006-hybrid-observability` | **Date**: 2026-11-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-hybrid-observability/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Give operators one Grafana (`lgtm`) with a hybrid split: **Prometheus → Ops/Infra** (is the search-index cluster broken: CPU, heap, shard/red alerts) and **Elasticsearch → Log/App** (why it broke: exact Catalog error logs). Cluster samples come from `elasticsearch-exporter` scraped by LGTM Prometheus (Method 1). Catalog Error logs also go to index `catalog-app-logs` via a fail-open Serilog sink; Loki stays. Catalog does not scrape Elasticsearch for cluster stats and does not `depends_on` exporter or Grafana. Isolated `dotnet test` does not start this stack. One compose pair. No new .csproj.

Technical approach: pin `quay.io/prometheuscommunity/elasticsearch-exporter`, mount an **extended** `/otel-lgtm/prometheus.yaml` (keep image scrape jobs + exporter job), provision Grafana ES data source + two dashboards + local alert rules under `deploy/grafana/`, add Catalog Serilog ES sink for Error+ only when compose sets it.

## Technical Context

**Language/Version**: C# / .NET 10.0 (Catalog + existing test projects)

**Primary Dependencies**: Existing Serilog + OTLP (`BuildBlock.Logging`). New Catalog package: `Elastic.Serilog.Sinks` (ES 8). Compose: existing `grafana/otel-lgtm:0.38.0`, Elasticsearch 8.15.3, new `elasticsearch-exporter`. Grafana provisioning YAML/JSON. Test: xUnit (existing suites)

**Storage**: Cluster samples = Prometheus inside LGTM. App errors for Log/App = Elasticsearch index `catalog-app-logs`. Product search index `catalog-products` unchanged. Isolated tests = no ES / no Prometheus.

**Testing**: Existing Catalog, BuildingBlock, and `BuildBlock.Logging.Tests` stay green without Grafana or the exporter. Optional isolated check of Error-document field names without a network. Compose quickstart proves both dashboards, alerts, and fail-open writes.

**Target Platform**: Local Docker Desktop compose + CI `dotnet test`. No production TLS/auth.

**Project Type**: Existing ASP.NET Catalog service + existing compose pair (no new project)

**Performance Goals**: Isolated suites unchanged (under 2 minutes). Log/App shows a failed-request Error within 2 minutes (SC-003). Ops/Infra has cluster samples after one minute of healthy ES (SC-002).

**Constraints**: One Grafana. One compose pair. Catalog start without exporter/LGTM/ES log sink. Do not rewrite logging test project. Do not alert on empty document count or single-node yellow replicas. Do not replace Kibana, Kafka UI, Loki Explore, or Catalog search meters. No Slack paging.

**Scale/Scope**: One exporter service, Prometheus scrape job, two Grafana dashboards, three local alert rules, one Serilog ES sink on Catalog compose. No new HTTP route.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` is still the unratified placeholder. Gates use that template’s example principles plus workspace SDD / TDD / simplicity rules.

| Gate | Result | Evidence |
|------|--------|----------|
| Library-first | PASS | No new library. Reuse `BuildBlock.Logging` Serilog host and existing LGTM/ES. Exporter is a compose sidecar, not a C# layer. |
| Test-first (non-negotiable) | PASS | Red: fail-open / document-shape isolated checks if any new C# is added; compose contracts in quickstart. Do not add production sink before a failing isolated check when C# changes. |
| Integration / contract tests | PASS | [contracts/ops-infra.md](./contracts/ops-infra.md), [log-app.md](./contracts/log-app.md), [compose-hybrid.md](./contracts/compose-hybrid.md). Isolated `dotnet test` + compose operator path. |
| Observability | PASS | This feature *is* observability. Fail-open Catalog. Isolated checks run with Grafana/exporter down. Loki/Tempo/Kibana kept. |
| Simplicity / abstraction ban | PASS | One exporter, one extra scrape job, provisioned datasources/dashboards. No Alloy, no second Prometheus, no new microservice. |
| CLI interface | N/A | Operators run documented `docker compose` and Grafana UI. |
| Spec-first | PASS | Spec at `specs/006-hybrid-observability/spec.md`. |

**Post-design re-check**: PASS. Phase 1 adds cluster sample + error document models, three operator contracts, and a `dotnet test` + compose quickstart. Catalog still starts if exporter/Grafana are down. Empty index and single-node yellow do not count as broken. Logging suite is not rewritten.

## Project Structure

### Documentation (this feature)

```text
specs/006-hybrid-observability/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── review-notes.md
├── contracts/
│   ├── ops-infra.md
│   ├── log-app.md
│   └── compose-hybrid.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
deploy/
├── compose.yaml                         # add elasticsearch-exporter
├── compose.override.yaml                # exporter env, lgtm volume mounts
├── prometheus/prometheus.yaml           # LGTM jobs + exporter scrape
└── grafana/provisioning/
    ├── datasources/elasticsearch.yaml
    ├── dashboards/
    └── alerting/

src/Services/eShop.Catalog.Api/
├── eShop.Catalog.Api.csproj             # Elastic.Serilog.Sinks
└── appsettings.json / compose env       # optional ES sink, Error+

src/BuildingBlocks/BuildBlock.Logging/   # unchanged unless fail-open needs a one-liner
tests/                                   # existing suites stay; no Testcontainers
README.md                                # start exporter + open Ops/Infra vs Log/App
```

**Structure Decision**: Extend the existing compose pair and Catalog Serilog config. Do not add a project. Do not put dashboards inside the logging test project. Do not add a second compose file.

## Complexity Tracking

> No constitution violations that require justification.
