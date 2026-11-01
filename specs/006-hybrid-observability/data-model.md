# Data Model: Hybrid Ops and App Observability

No new Catalog public HTTP resources. Two operator views over existing signals.

## SearchIndexClusterSample

Prometheus time series scraped from `elasticsearch-exporter`. Not stored by Catalog.

| Field | Source metric (typical) | Required | Rules |
|-------|-------------------------|----------|--------|
| Health | `elasticsearch_cluster_health_status` | yes | Ops/Infra. Alert only on **red**. Yellow allowed on local single-node. |
| Node count | `elasticsearch_cluster_health_number_of_nodes` | yes | Display. |
| CPU | `elasticsearch_process_cpu_percent` | yes | Alert > 90% for 5 minutes. |
| Heap used / max | `elasticsearch_jvm_memory_used_bytes{area="heap"}`, `..._max_bytes` | yes | Alert when used/max > 0.90 for 2 minutes. |
| Disk | `elasticsearch_filesystem_data_*` | no | Display when exporter provides it. |
| Unassigned shards | `elasticsearch_cluster_health_unassigned_shards` | yes | Display. Do not alert solely because replicas are unassigned. |
| Documents | `elasticsearch_cluster_health_number_of_documents` | yes | Display. Zero is valid (empty catalog). Must not alert. |

### Lifecycle

```text
ES up → exporter scrape 15s → Prometheus → Ops/Infra panels + local Grafana alerts
ES down → scrape fails / stale → Ops/Infra shows broken; Catalog writes still succeed
Empty index → documents=0, health green or yellow → no cluster-down alert
```

## CatalogErrorDocument

One Elasticsearch document in index `catalog-app-logs` (logical). Written by Catalog Serilog on **Error** and **Fatal** only.

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| `@timestamp` | date-time | yes | Event time (UTC). Grafana time field. |
| `message` | string | yes | Failed-request text: request name, duration, stack (existing 005 rule). |
| `level` | string | yes | `Error` or `Fatal`. |
| `exception` | string or object | no | Exception type + stack when present. |
| `service.name` | string | yes | `eShop.Catalog.Api`. Log/App filter. |
| `deployment.environment` | string | no | Existing enricher. |
| `trace_id` / `span_id` | string | no | When a request activity exists. |

Document id is assigned by Elasticsearch. No Catalog-store dual-write of products.

### Lifecycle

```text
Failed Catalog request → existing Error log → Serilog ES sink (compose only) → catalog-app-logs
ES sink down → log still goes to console + Loki; Catalog process stays up
Isolated test host → no ES sink → no document; tests still pass
```

## OpsInfraDashboard

Grafana dashboard. Prometheus data source only.

| Panel / item | Answers |
|--------------|---------|
| Cluster health | Broken vs live |
| CPU / heap | Pressure |
| Shards | Red / unassigned primaries |
| Document count | Empty vs populated (not an alert) |
| Alert list | CPU, heap, red cluster |

## LogAppDashboard

Grafana dashboard. Elasticsearch data source only (`catalog-app-logs*`, `@timestamp`).

| Panel / item | Answers |
|--------------|---------|
| Error log table / volume | Why it broke |
| Filter `service.name` = Catalog | Catalog only |

## Relationships

```text
Search-index cluster ──scraped by──► elasticsearch-exporter ──scraped by──► Prometheus ──► Ops/Infra
Catalog Error log ──Serilog──► Loki (existing) 
                 └──Serilog──► catalog-app-logs ──► Elasticsearch data source ──► Log/App
```
