# Review notes: Hybrid observability (how it works + how to implement)

Date: 2026-11-01  
Spec: [006-hybrid-observability](./spec.md) · Tasks: [tasks.md](./tasks.md)  
Grafana: http://localhost:3000 (admin / admin)

One Grafana (`lgtm`). Two questions, two stores. Catalog product work does not wait on either.

---

# Part A — How it works

```
                    ┌─────────────────────────────────────────────────────────┐
                    │                     Grafana (lgtm)                      │
                    │  Ops/Infra  ← Prometheus     Log/App ← Elasticsearch   │
                    │  “is it broken?”             “why it broke?”            │
                    └──────────────▲────────────────────────────▲─────────────┘
                                   │                            │
                    elasticsearch-exporter                 catalog-app-logs
                    :9114 /metrics                         (Error / Fatal only)
                                   │                            │
                                   └──────── Elasticsearch ─────┘
                                             (search index unchanged)
```

## A1. The split

| Dashboard | Data source | Answers | Does **not** do |
|-----------|-------------|---------|-----------------|
| **Ops/Infra** | Prometheus only | Cluster live vs red, CPU, heap, shards | Show Catalog exception text |
| **Log/App** | Elasticsearch only (`catalog-app-logs*`) | Failed-request Error text (name, duration, stack) | Show cluster CPU/heap |

Loki Explore stays. Kibana stays for `catalog-products`. No second Grafana. No second compose file.

## A2. Ops/Infra — cluster metrics

1. Elasticsearch is up (`http://elasticsearch:9200`, no auth).
2. `elasticsearch-exporter` (`v1.8.0`) uses `--es.uri=http://elasticsearch:9200` on **9114**.
3. Prometheus inside `lgtm` scrapes `elasticsearch-exporter:9114/metrics` (job `elasticsearch`).
4. **Ops/Infra** uses Prometheus uid `prometheus` (already in the image). Do not add a data source.

LGTM’s default Prometheus file is OTLP settings. Replacing it with scrape-only would drop self/OTLP metrics. `deploy/prometheus/prometheus.yaml` is a **copy of that default** plus job `elasticsearch`.

| Panel | PromQL | Role |
|-------|--------|------|
| Cluster health | `elasticsearch_cluster_health_status` | `1` on the current color (`green` / `yellow` / `red`) |
| CPU | `elasticsearch_process_cpu_percent` | Pressure |
| Heap | used/max `{area="heap"}` | Pressure |
| Unassigned shards | `elasticsearch_cluster_health_unassigned_shards` | Display only |
| Documents | `elasticsearch_cluster_health_number_of_documents` | Display only. **0 is valid** |

Local alerts (`es-cluster.yaml`): red cluster; heap > 90% for **2m**; CPU > 90% for **5m**. No alert on 0 docs or yellow replicas.

`curl -s http://localhost:9114/metrics` should include `elasticsearch_cluster_health_`.

## A3. Log/App — Catalog errors

1. MediatR already logs Error (name, duration, stack).
2. Compose sets `Serilog__Elasticsearch__Nodes__0=http://elasticsearch:9200`.
3. `AddCatalogErrorLogSink()` **before** `AddAppLogging()` registers `ILogEventSink` only when Nodes is set.
4. Isolated tests omit Nodes → no sink.
5. Error/Fatal only → data stream `catalog-app-logs`.
6. Log/App uses uid `elasticsearch`, index `catalog-app-logs*`, `@timestamp`.

Console + OTLP → Loki still get the Error.

Isolated mapper fields: `@timestamp`, `message`, `level` (`Error`/`Fatal`), `service.name`, optional exception / trace / span. Live ECS uses `log.level` and `service.name`. Log/App queries both. Empty result ≠ broken data source.

Fail-open: `BootstrapMethod.Silent`, swallow write errors, no `ValidateOnStart`.

## A4. Fail-open Catalog

`catalogapi` `depends_on` **`catalogdb` only**.

| Situation | Create / import / get-by-id | `GET /products` |
|-----------|-----------------------------|-----------------|
| Exporter or Grafana stopped | Works | Unchanged (search uses ES, not Grafana) |
| ES log sink cannot write | Works; Error on console/Loki | Unchanged |
| Isolated test host | Works | In-memory search |

Verified: stop exporter + lgtm → POST 201, GET by id 200.

## A5. Grafana mounts (do not wipe Loki/Tempo)

Extra files only — never mount over the whole provisioning directory.

## A6. Mental model

- **Yellow + 0 documents** = single-node / empty catalog. Not “down.”
- **Red / heap / CPU alerts** = Ops/Infra.
- **Exception text** = Log/App and Loki Explore.
- **Cannot list products after import** = CDC (`catalog-products`). See [005 review notes](../005-catalog-page-entities/review-notes-get-products.md).

---

# Part B — How to implement (detail)

Rules: one compose pair, one Grafana, Catalog starts without exporter/Grafana, isolated tests do not start Docker, TDD for new C#, do not edit `BuildBlock.Logging.Tests`.

## B0. Order

```
1. Folders
2. Exporter + extended Prometheus + compose mounts
3. Failing isolated tests (alerts, error document, fail-open)
4. Ops/Infra dashboard + alert YAML
5. Catalog Error sink + Log/App + ES data source
6. Confirm catalogapi depends_on catalogdb only
7. README + operator check
```

Do not start dashboards before exporter scrape works.

## B1. Folders

```
deploy/prometheus/
deploy/grafana/provisioning/datasources/
deploy/grafana/provisioning/dashboards/
deploy/grafana/provisioning/alerting/
```

`.gitkeep` in empty folders is harmless once JSON exists.

## B2. Exporter

`compose.yaml`:

```yaml
elasticsearch-exporter:
  image: quay.io/prometheuscommunity/elasticsearch-exporter:v1.8.0
```

`compose.override.yaml`: `eshop_elasticsearch_exporter`, `--es.uri=http://elasticsearch:9200`, `9114:9114`, `depends_on` Elasticsearch healthy, wget healthcheck on `/healthz`.

`catalogapi.depends_on` stays `catalogdb` only. OTLP env may mention `lgtm:4318` — that is not a start dependency.

```bash
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait \
  elasticsearch elasticsearch-exporter
curl -s http://localhost:9114/metrics | findstr elasticsearch_cluster_health_
```

## B3. Extend Prometheus

Copy LGTM 0.38.0 default `docker/prometheus.yaml`, then append:

```yaml
scrape_configs:
  - job_name: elasticsearch
    metrics_path: /metrics
    static_configs:
      - targets: ["elasticsearch-exporter:9114"]
```

Target is the **compose service name**. Mount as `/otel-lgtm/prometheus.yaml:ro`.

Explore → **Prometheus**: `elasticsearch_cluster_health_status`.

## B4. Extra Grafana files only

| Host | Container |
|------|-----------|
| `datasources/elasticsearch.yaml` | `.../datasources/elasticsearch.yaml` |
| `dashboards/dashboards.yaml` | `.../dashboards/eshop-dashboards.yaml` (new name) |
| `ops-infra.json` / `log-app.json` | `/otel-lgtm/eshop-dashboards/` |
| `alerting/` | `.../provisioning/alerting/` |
| `prometheus.yaml` | `/otel-lgtm/prometheus.yaml` |

Provider path: `/otel-lgtm/eshop-dashboards`. After recreate: Prometheus + Loki + Elasticsearch still listed.

## B5. Ops/Infra + alerts

Test first: `HybridObservabilityRulesTests.cs` loads `es-cluster.yaml` and asserts red / heap 0.90 2m / CPU > 90 5m; not docs, yellow, slack, email. `RepoRoot` walks to `eShop.slnx`.

Dashboard `ops-infra.json`: title `Ops/Infra`, uid `eshop-ops-infra`, datasource `prometheus`. Open http://localhost:3000/d/eshop-ops-infra/ops-infra (Last 15 minutes).

Alerts: `es-cluster-red` `for: 0m`; heap `2m`; CPU `5m`; `noDataState: OK`.

## B6. Catalog Error sink + Log/App

Tests first: `CatalogErrorDocumentTests` (Error/Fatal map; Info/Debug do not). `HybridObservabilityFailOpenTests` (create/get-by-id; compose `depends_on`).

Package on Catalog only: `Elastic.Serilog.Sinks` 8.18.2.

`CatalogErrorDocument.TryMap` then `AddCatalogErrorLogSink` **before** `AddAppLogging()`. Nodes empty in `appsettings.json`. Compose: `Serilog__Elasticsearch__Nodes__0=http://elasticsearch:9200`. `BootstrapMethod.Silent`. Data stream `catalog-app-logs`.

ES data source: uid `elasticsearch`, `catalog-app-logs*`, `@timestamp`, `logLevelField: log.level`. Log/App uid `eshop-log-app`; query `service.name` and `log.level`/`level`.

## B7. Fail-open + tests

```bash
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml config
# catalogapi.depends_on = catalogdb only

dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj
dotnet test tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj
dotnet test tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj

docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml stop elasticsearch-exporter lgtm
# POST /products → 201, GET /products/{id} → 200
```

## B8. Recreate after file changes

```bash
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait --force-recreate lgtm
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait --build catalogapi
```

## B9. Where to look

| You want | Open |
|----------|------|
| ES + Kafka + Catalog.Api metrics | http://localhost:3000/d/eshop-ops-infra/ops-infra |
| Raw series | Explore → Prometheus → `elasticsearch_cluster_health_status` |
| Catalog exception | http://localhost:3000/d/eshop-log-app/log-app |
| Same logs in Loki | Explore → Loki → `{service_name=~".*eShop.Catalog.Api.*"}` |
| Exporter | `curl http://localhost:9114/metrics` |
| Product search docs | Kibana `catalog-products` |

Home / RED / JVM are image defaults — they do not show exporter series.

## B10. File map

| File | Role |
|------|------|
| `deploy/compose.yaml` | Exporter image |
| `deploy/compose.override.yaml` | Exporter + lgtm mounts + Catalog Nodes env |
| `deploy/prometheus/prometheus.yaml` | LGTM default + job `elasticsearch` |
| `deploy/grafana/provisioning/...` | ES source, dashboards, alerts |
| `eShop.Catalog.Api` Logging + csproj | Sink + document shape |
| Catalog tests | Rules, document, fail-open |

## B11. Decisions (do not undo)

| Decision | Why |
|----------|-----|
| Exporter + Prometheus | Catalog does not scrape `/_cluster/health` |
| Extend Prometheus YAML | Replacing drops LGTM OTLP jobs |
| Extra Grafana files | Keeps Loki/Tempo |
| Alert red only, not yellow | Single-node yellow is normal |
| Do not alert on 0 docs | Empty catalog is valid |
| Error/Fatal only to ES | Log/App is “why it broke” |
| Sink only when Nodes is set | Isolated tests stay offline |
| Silent bootstrap + catch | Catalog starts if ES is down |

Do not add Slack, TLS, a second compose file, dual-write of products, or new Catalog HTTP routes.
