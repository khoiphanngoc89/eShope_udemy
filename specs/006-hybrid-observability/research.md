# Research: Hybrid Ops and App Observability

## 1. Cluster metrics use Prometheus + Elasticsearch exporter (Method 1)

**Decision**: Add compose service `elasticsearch-exporter` (`quay.io/prometheuscommunity/elasticsearch-exporter`, pin a 1.8.x or current stable tag). Command: `--es.uri=http://elasticsearch:9200`. Listen on `9114`. Prometheus inside existing `lgtm` scrapes `http://elasticsearch-exporter:9114/metrics`. Catalog does not scrape Elasticsearch for cluster stats.

**Rationale**: FR-002, FR-004. Spec requested Method 1. The exporter already exposes cluster health, JVM heap, process/OS CPU, and shard gauges. One extra compose service, no new .csproj.

**Alternatives considered**:
- Metricbeat / Elastic official monitoring — extra product; rejected by Method 1.
- Grafana Alloy `prometheus.exporter.elasticsearch` — second collector next to LGTM; heavier than one exporter + scrape.
- Catalog HTTP-scrapes `/_cluster/health` — rejected by FR-004.

## 2. LGTM Prometheus scrape without a second metrics viewer

**Decision**: Keep `grafana/otel-lgtm`. Mount an **extended** Prometheus config at `/otel-lgtm/prometheus.yaml` that retains the image’s existing scrape jobs and adds job `elasticsearch` → `elasticsearch-exporter:9114`. Do not replace the file with a scrape-only config (that drops OTLP/self metrics). Implementation copies the image default first, then appends the job. Grafana stays the one metrics viewer (FR-009).

**Rationale**: docker-otel-lgtm documents that mount path. FR-001 / FR-009: one viewer, no second Grafana.

**Alternatives considered**:
- Second Prometheus container — extra viewer/store; rejected.
- `PROMETHEUS_EXTRA_ARGS` only — cannot add scrape targets.
- Manual Grafana data-source UI — rejected by “no data source by hand.”

## 3. Ops/Infra signals and local alerts

**Decision**: Ops/Infra dashboard and Grafana-managed local alerts use exporter metrics:

| Signal | Metric (typical) | Local alert |
|--------|------------------|-------------|
| Cluster health | `elasticsearch_cluster_health_status` | Fire when `red` |
| Heap | `elasticsearch_jvm_memory_used_bytes{area="heap"}` / `..._max_bytes` | Fire when > 90% for 2m |
| CPU | `elasticsearch_process_cpu_percent` (or OS CPU if process missing) | Fire when > 90% for 5m |
| Shards | `elasticsearch_cluster_health_unassigned_shards` / relocating | Fire when **unassigned primary** or cluster `red`. Do **not** fire on yellow replica unassigned (normal on single-node). |
| Docs | `elasticsearch_cluster_health_number_of_documents` (or indices docs) | Display only. **Zero documents must not alert.** |

**Rationale**: FR-002, FR-003, SC-006. Local compose ES is one node with replicas often unassigned → yellow is healthy-empty or healthy-with-replicas, not “broken.”

**Alternatives considered**:
- Alert on any non-green status — false positives on local single-node.
- Alert on document count 0 — rejected by FR-003 / empty-index edge case.
- Slack/email paging — out of spec.

## 4. Grafana data sources and dashboards

**Decision**: Provision in the same Grafana (`lgtm:3000`, admin/admin):

- Prometheus data source — already provided by LGTM. Ops/Infra uses it. Uid stays the image default (`prometheus` / existing).
- Elasticsearch data source — provisioned YAML, url `http://elasticsearch:9200`, index `catalog-app-logs*`, time field `@timestamp`, no auth. Uid `elasticsearch`.
- Folder/dashboards: `Ops / Infra` (Prometheus panels + alert list) and `Log / App` (Elasticsearch logs panel filtered to Catalog errors).
- Mount provisioning files from `deploy/grafana/` so operators never add a data source by hand.

If a full provisioning-directory mount would wipe Loki/Tempo sources, add a **single extra** datasource file into the image’s existing datasources folder (inspect path at implement time). Same rule for dashboards.

**Rationale**: FR-001, FR-005, FR-011. Hybrid split in one viewer.

**Alternatives considered**:
- Kibana as Log/App — Kibana stays for `catalog-products` documents (FR-010). Log/App is Grafana + ES data source.
- Loki as Log/App — rejected by requested hybrid (ES data source for why-it-broke). Loki Explore remains.

## 5. Catalog errors land in Elasticsearch without replacing Loki

**Decision**: Catalog compose enables a Serilog Elasticsearch sink (package on `eShop.Catalog.Api`, e.g. `Elastic.Serilog.Sinks` matching ES 8). Index `catalog-app-logs`. Minimum level **Error**. Existing OpenTelemetry → Loki sink stays. Isolated `dotnet test` does **not** set the ES sink. Catalog starts if Elasticsearch is down (fail-open sink / no `ValidateOnStart` on the sink). Do not rewrite `BuildBlock.Logging.Tests`.

Failed-request logs already include request name, duration, and stack (005 FR-021). Those Error events are what Log/App shows.

**Rationale**: FR-005, FR-006, FR-007, FR-008. Ponytail: reuse Serilog `WriteTo` config; no new logging project.

**Alternatives considered**:
- Remove Loki — rejected; spec keeps Loki Explore.
- Dual-write every Information log — noise; Log/App is errors/exceptions.
- Filebeat sidecar — extra container; Catalog already has Serilog.
- Change `AddAppLogging` to always require ES — would break isolated tests and fail-open start.

## 6. Compose layout and fail-open

**Decision**: Extend the existing compose pair only.

| Service | Role | Catalog `depends_on`? |
|---------|------|------------------------|
| `elasticsearch-exporter` | Cluster metrics | No |
| `lgtm` | Prometheus + Grafana (+ Loki/Tempo) | No |
| `elasticsearch` | Search index + log index | No (already true) |

`elasticsearch-exporter` waits for `elasticsearch` healthy. `catalogapi` still waits only for `catalogdb`. Isolated tests start none of these.

**Rationale**: FR-007, FR-008, FR-009.

**Alternatives considered**: Second compose file — rejected by FR-009. Catalog `depends_on` exporter — rejected by FR-007.

## 7. Isolated tests

**Decision**: No Testcontainers. No live Grafana. Prove fail-open with existing logging and Catalog suites (still green without ES/LGTM). Add a small isolated check that the Error log **document shape** (timestamp, message, exception, `service.name`) can be serialized for the ES index — in Catalog or BuildingBlock tests without a network. Compose quickstart proves dashboards and alerts.

**Rationale**: FR-008, SC-004. Same rule as 005.

**Alternatives considered**: Testcontainers Grafana — too slow and not required for isolated checks.
