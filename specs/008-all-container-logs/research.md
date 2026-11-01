# Research: All Container Logs Without App Duplicates

## 1. Collector coverage: default-collect the stack, drop application exporters

**Decision**: Keep the existing `log-collector` (`grafana/alloy:v1.21.3`). Change `deploy/alloy/config.alloy` from a two-name keep-list (`eshop_catalog_api`, `eshop_kafka`) to **default-collect** every container on the compose network `eshop`, then **drop** application-exporting services. First drop entry: `catalogapi` / `eshop_catalog_api`. Also drop `log-collector` and `lgtm` (collector/viewer self-logs are optional; a collector loop is not required). Collapse discovery to **one target per container** (keep network `eshop`; drop extra port-mapping targets if Alloy emits them).

Set operator label `compose_service` from `__meta_docker_container_label_com_docker_compose_service` (Alloy sanitizes dots to underscores). Keep optional `container` from `__meta_docker_container_name`. Loki write stays `http://lgtm:3100/loki/api/v1/push`. Do not consume Kafka topics. Do not tail Kafka data-dir files.

**Rationale**: FR-001 / FR-011. A keep-list of today’s names would miss the next exporter or UI. Catalog already exports via OTLP (002); 007’s Catalog stdout tail is the duplicate this spec forbids.

**Log identities to collect** (after each is healthy and has written stdout):

| compose_service | container | Role |
|-----------------|-----------|------|
| `catalogdb` | `eshop_catalog_db` | store process |
| `kafka` | `eshop_kafka` | broker process |
| `connect` | `eshop_debezium_connect` | connector |
| `connect-register` | `eshop_debezium_register` | connector register (optional log) |
| `elasticsearch` | `eshop_elasticsearch` | search index |
| `kibana` | `eshop_kibana` | operator UI |
| `kafka-ui` | `eshop_kafka_ui` | operator UI |
| `elasticsearch-exporter` | `eshop_elasticsearch_exporter` | metric exporter |
| `kafka-exporter` | `eshop_kafka_exporter` | metric exporter |
| `postgres-exporter` | `eshop_postgres_exporter` | metric exporter (this slice) |
| `cadvisor` | `eshop_cadvisor` | container process-up (this slice) |

**Drop (do not ship a container copy)**:

| compose_service | Why |
|-----------------|-----|
| `catalogapi` | Application diagnostics already go OTLP → Loki (`{service_name=~".*eShop.Catalog.Api.*"}`) |
| `log-collector` | Optional; avoid collector-collects-itself; not on the health board |
| `lgtm` | Optional; not on the health board |

A later application service that exports the same way as Catalog is added to this drop-list (FR-011). Isolated 007 tests that require Alloy to keep `eshop_catalog_api` must be rewritten in implementation.

**Alternatives considered**:
- Keep-list every current non-app container — every new UI/exporter is a miss.
- Collect Catalog stdout and de-duplicate in Loki — two paths, rejected by FR-003.
- Docker `logging: loki` driver — extra plugin; rejected in 007.
- Second collector or second Loki — rejected by FR-007 / FR-008.

## 2. Catalog lines appear once (application path only)

**Decision**: Catalog request diagnostics in Grafana come **only** from the existing application export (Serilog OTLP → `lgtm:4318` → Loki). Alloy MUST NOT tail `eshop_catalog_api`. Explore selector for Catalog stays `{service_name=~".*eShop.Catalog.Api.*"}`. Remove README / 007 LogQL that treats `{compose_service="catalogapi"}` as the Catalog request path. Elasticsearch Log/App (Error+) stays.

Catalog.Api **does** appear on the Ops/Infra health overview (process up). That is metrics, not a second log stream.

**Rationale**: FR-003, SC-002. The duplicate is Catalog console text listed twice (OTLP + Alloy).

**Alternatives considered**:
- Keep Alloy Catalog tail and turn off OTLP — breaks 002 Explore / Tempo.
- Accept overlap as 007 did — rejected by this spec.

## 3. Process-up for the product-stack overview: cAdvisor scrape

**Decision**: Add compose service `cadvisor` (`gcr.io/cadvisor/cadvisor` or `ghcr.io/google/cadvisor`, pin a published v0.49+ tag at implement). Mount Docker socket read-only (same class of mount as Alloy). LGTM Prometheus scrapes `cadvisor:8080` as job `containers`. Relabel Docker compose metadata to `compose_service`. Do **not** put cAdvisor inside Alloy (avoids privileging the log collector and does not need Prometheus remote_write on LGTM). Do **not** require `log-collector` or `lgtm` on the overview.

**Healthy** = that product-stack container’s process is up (a running-container series exists for its `compose_service`). **Stopped or missing** = no running series → not healthy (dashboard always lists the expected identities so a missing Kibana still appears). Empty catalog size is **not** this signal.

**Overview inventory (always listed)**:

| compose_service | Process-up | Detail when clicked |
|-----------------|------------|---------------------|
| `catalogdb` | cAdvisor running | CPU, RAM, plus `pg_up`, `pg_stat_database_numbackends{datname="catalog"}`, `pg_database_size_bytes{datname="catalog"}` |
| `catalogapi` | cAdvisor running | CPU, RAM, plus existing Catalog.Api request/search/sync panels |
| `kafka` | cAdvisor running | CPU, RAM, plus existing Kafka broker/topic/lag panels |
| `connect` | cAdvisor running | CPU and RAM |
| `elasticsearch` | cAdvisor running | CPU, RAM, plus existing ES cluster/heap/docs panels |
| `kibana` | cAdvisor running | CPU and RAM |
| `kafka-ui` | cAdvisor running | CPU and RAM |
| `elasticsearch-exporter` | cAdvisor running | CPU and RAM |
| `kafka-exporter` | cAdvisor running | CPU and RAM |
| `postgres-exporter` | cAdvisor running | CPU and RAM |

`connect-register`, `log-collector`, and `lgtm` are **not** required on the board.

**Why not Alloy `prometheus.exporter.cadvisor`**: It needs extra host mounts / privileged mode and typically remote_writes into Prometheus. LGTM’s scrape file is the existing 006 pattern; a cadvisor sidecar matches elasticsearch-exporter / kafka-exporter. Alloy on Docker Desktop for Windows is also a weaker host-cgroup path.

**Why not exporter `up` only**: If `postgres-exporter` is down while `catalogdb` is up, exporter scrape `up` would lie about the store process. cAdvisor answers “is the container running?”

**Rationale**: FR-012, SC-007. One Grafana. Fixed inventory so missing shows as not healthy.

**Alternatives considered**:
- Alloy embedded cAdvisor + remote_write — extra flags/mounts on the log collector; LGTM remote_write not guaranteed.
- Blackbox TCP probes — another product; ports vary; still not “container running” if a port is closed on a live process.
- Only store/ES/Kafka/Catalog.Api on the board — rejected by clarify (product stack).

## 4. Store detail: postgres-exporter (unchanged hybrid)

**Decision**: Add `postgres-exporter` (`quay.io/prometheuscommunity/postgres-exporter`, pin v0.17+ at implement). `DATA_SOURCE_NAME=postgresql://catalog:catalog@catalogdb:5432/catalog?sslmode=disable`. Listen `9187`. `depends_on` `catalogdb` healthy. Catalog MUST NOT scrape the store and MUST NOT `depends_on` the exporter, cadvisor, or `lgtm`.

Prometheus job `catalogdb` → `postgres-exporter:9187`. Do **not** enable `--collector.stat_statements` or `include_query`.

These signals are **store detail** after click, not the overview healthy bit (overview healthy is cAdvisor process-up). Small or zero `pg_database_size_bytes` is not not-healthy.

**Rationale**: FR-013 / FR-014. Same Method 1 as 006.

**Alternatives considered**: Catalog scrape of `pg_stat_*` — rejected. Enable `stat_statements` — SQL text; rejected.

## 5. One Ops/Infra dashboard: overview + click-through detail

**Decision**: Evolve the existing dashboard `deploy/grafana/provisioning/dashboards/ops-infra.json` (title **Ops/Infra**, uid `eshop-ops-infra`). Do **not** add a second dashboard uid or a second Grafana.

Layout:

1. **Overview** (top): fixed product-stack identities as healthy / not healthy. One table or stat grid. Data links on each identity open **the same dashboard** with template variable `service` set (Grafana data link `var-service=<compose_service>`).
2. **Detail** (below): panels for `$service`. Every identity shows CPU and RAM. Store (`catalogdb`) also shows up / connections / size. `elasticsearch`, `kafka`, and `catalogapi` keep the richer 006 panels as that identity’s detail.
3. Keep the “empty catalog is not an outage” note. 006 ES local alerts stay; do not alert on size 0.

Log search stays Explore → Loki (and Log/App for Catalog errors). The health board does not replace Explore.

**Rationale**: Clarify 2026-11-01: one overview, click for detail, same Grafana.

**Alternatives considered**:
- Second “Stack health” dashboard plus unchanged Ops/Infra — two metrics boards; rejected.
- Flat 006 page with extra store panels only — rejected by clarify.
- Custom Catalog HTTP health UI — new route; rejected by FR-004.

## 6. Fail-open and tests

**Decision**: `catalogapi` still `depends_on` `catalogdb` only. Product services do not `depends_on` `log-collector`, `postgres-exporter`, `cadvisor`, or `lgtm`. Isolated `dotnet test` parses compose + Alloy + Prometheus + Ops/Infra JSON (inventory, data links, `service` variable, drop-list, no `stat_statements`). No Testcontainers. No new Catalog C#. Do not edit `BuildBlock.Logging.Tests`.

**Rationale**: FR-005, FR-006, SC-003, SC-004.

## 7. Compose layout and run notes

**Decision**: Extend `deploy/compose.yaml` + `deploy/compose.override.yaml` only. New services: `postgres-exporter`, `cadvisor`. Alloy file changes in place. Prometheus jobs: `catalogdb`, `containers`. Ops/Infra JSON becomes overview + detail. README: start collector + cadvisor + exporter with Grafana; LogQL by `compose_service`; Catalog is OTLP once; how to read the overview and click for detail.

**Rationale**: FR-007, FR-008, FR-009.

**Alternatives considered**: Second compose file or second Grafana — rejected.
