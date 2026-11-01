# Contract: Ops/Infra (Prometheus)

Operator contract. Not a Catalog HTTP API. `dotnet test` does not start Grafana or the exporter.

Auth: local Grafana **admin** / **admin**.

## 1. Data source (FR-001)

After `lgtm` is up, Grafana already has Prometheus. Operators do not add it.

**Success**

- Explore → Prometheus works without a new data source.
- Ops/Infra dashboard uses that Prometheus source only.

## 2. Signals (FR-002, SC-002)

With Elasticsearch, Kafka, and Catalog.Api up for at least one minute, a recent range (last 15 minutes) includes samples for search-index cluster health, Kafka broker/topic/lag, and Catalog.Api request/search metrics. One Ops/Infra dashboard. Prometheus only.

**PromQL (local, documented in README / dashboard)**

```promql
elasticsearch_cluster_health_status
elasticsearch_process_cpu_percent
elasticsearch_jvm_memory_used_bytes{area="heap"}
/ elasticsearch_jvm_memory_max_bytes{area="heap"}
elasticsearch_cluster_health_unassigned_shards
elasticsearch_cluster_health_number_of_documents
kafka_brokers
kafka_topic_partitions
kafka_consumergroup_lag
http_server_request_duration_seconds_count{service_name="eShop.Catalog.Api"}
catalog_search_duration_milliseconds_count{service_name="eShop.Catalog.Api"}
catalog_search_sync_lag_seconds{service_name="eShop.Catalog.Api"}
```

**Failure of the feature**

- Operator must add a Prometheus data source by hand.
- Empty document count fires a cluster-down alert.
- Single-node yellow (unassigned replicas) fires a “broken” alert.

## 3. Alerts (FR-003)

Local Grafana alert rules (no Slack/email):

| Alert | Fire when |
|-------|-----------|
| Cluster red | health status is red |
| Heap pressure | heap used/max > 90% for 2 minutes |
| CPU pressure | process CPU > 90% for 5 minutes |

**Success**: operator sees the firing state on Ops/Infra. Empty index does not fire these.

## 4. Fail-open (FR-007)

Catalog create / import / get-by-id succeed if exporter or Prometheus is stopped.
