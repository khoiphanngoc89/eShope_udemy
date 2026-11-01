# Data Model: All Container Logs Without App Duplicates

No new Catalog public HTTP resources. Operator views over process stdout, plus a product-stack health overview and click-through detail metrics.

## ContainerProcessLine

One Loki stream entry collected from a **non-application** stack container’s stdout/stderr.

| Field / label | Required | Rules |
|---------------|----------|--------|
| `compose_service` | yes | Compose service name (`catalogdb`, `kafka`, `connect`, `elasticsearch`, `kibana`, `kafka-ui`, exporters, …). Filter key vs other containers. |
| `container` | no | Docker name when set. |
| timestamp | yes | Line time (Docker / Loki). |
| line body | yes | Process text. **Not** a Kafka topic message body. **Not** a copy of Catalog application diagnostics. |

### Lifecycle

```text
Stack container stdout → Docker json-file → Alloy (network eshop, drop app list) → Loki → Explore
Collector down → line stays on `docker logs`; process stays up
Container up before collector → only new lines after collector starts (no backfill required)
```

### Validation

- Empty last-15-minutes is valid, not a data-source error.
- `{compose_service="kafka"}` must not return Catalog application lines.
- `{compose_service="catalogapi"}` must not be a populated collector stream.

## ApplicationDiagnosticLine

One Catalog (or later application service) log event already sent by the application export path.

| Field / label | Required | Rules |
|---------------|----------|--------|
| `service_name` | yes | `eShop.Catalog.Api`. Explore: `{service_name=~".*eShop.Catalog.Api.*"}`. |
| timestamp | yes | Event time. |
| line body | yes | Request / diagnostic text. Failed-request Errors include request name, duration, stack when recorded. |
| `trace_id` / `span_id` | no | Present on request rows when ASP.NET has an activity. |

### Lifecycle

```text
Catalog Serilog → OTLP :4318 → Loki (and Error+ → Elasticsearch Log/App)
Alloy MUST NOT also tail catalogapi stdout
```

### Validation

- After one Catalog request, the same request line is listed once, not twice from OTLP + container tail.

## ContainerHealthStatus

One overview reading per **product-stack** identity on Ops/Infra.

| Field | Required | Rules |
|-------|----------|--------|
| `compose_service` | yes | One of: `catalogdb`, `catalogapi`, `kafka`, `connect`, `elasticsearch`, `kibana`, `kafka-ui`, `elasticsearch-exporter`, `kafka-exporter`, `postgres-exporter`. |
| healthy | yes | **Healthy** = that container’s process is up (running-container series exists). **Not healthy** = stopped or missing (no running series). Empty catalog data is **not** this field. |
| on board | yes | Identity is always listed even when missing. `log-collector` and `lgtm` are not required. |

### Lifecycle

```text
Product-stack container running → cadvisor → Prometheus job containers → Ops/Infra overview (healthy)
Stopped or never started → no running series → overview shows not healthy (not a Grafana config error)
cadvisor / Grafana down → Catalog create / get-by-id still succeed
```

## ContainerDetailMetrics

Metrics shown after the operator clicks an overview identity (same Grafana, variable `service`).

| `service` | Required detail | Forbidden |
|-----------|-----------------|-----------|
| every product-stack id | CPU and RAM | — |
| `catalogdb` | Store health sample: up, connections, size, plus CPU and RAM | SQL text / per-query payloads; size 0 as outage |
| `catalogapi` | Existing Catalog.Api request / search / sync signals, plus CPU and RAM | — |
| `kafka` | Existing Kafka broker / topic / lag signals, plus CPU and RAM | Topic message bodies |
| `elasticsearch` | Existing ES cluster / heap / CPU / docs signals, plus container CPU and RAM | Docs 0 as cluster-down |
| any other product-stack id | CPU and RAM (and process-up) | — |

## StoreHealthSample

The catalog store’s **detail** metrics (not the overview healthy bit).

| Field | Required | Rules |
|-------|----------|--------|
| up | yes | `pg_up` (fallback `up{job="catalogdb"}`). |
| connections | yes | `pg_stat_database_numbackends{datname="catalog"}`. |
| size | yes | `pg_database_size_bytes{datname="catalog"}`. 0 or small is a valid empty catalog, not not-healthy. |
| SQL text / per-query payload | no | **Forbidden.** `stat_statements` stays off. |

## LogCollector

Existing compose sidecar.

| Attribute | Rules |
|-----------|--------|
| Discover | Docker socket, network `eshop`, one target per container |
| Drop | `catalogapi`, `log-collector`, `lgtm` |
| Keep | Other current stack services (including store, broker, connector, UIs, exporters, cadvisor) |
| Sink | Loki in `lgtm` (`/loki/api/v1/push`) |
| Out of band | Kafka topic consume, Kafka data-dir tail, Catalog HTTP scrape |

## Cadvisor

Compose sidecar. Process-up for the overview.

| Attribute | Rules |
|-----------|--------|
| Sink | Prometheus in `lgtm` (`job_name: containers`) |
| Catalog coupling | None. Catalog does not scrape. Catalog does not wait. |
| Board | Used for healthy/not. Not listed as a required overview identity. |

## PostgresExporter

Compose sidecar.

| Attribute | Rules |
|-----------|--------|
| Target | `catalogdb` / database `catalog` |
| Sink | Prometheus in `lgtm` (`job_name: catalogdb`) |
| Catalog coupling | None |
| Board | Appears on the overview as `postgres-exporter` (process-up). Store **detail** uses its gauges. |

## Relationships

```text
Non-app stdout ──► log-collector ──► Loki ──► Explore {compose_service=…}
Catalog app     ──► OTLP :4318     ──► Loki ──► Explore {service_name=…}
Catalog Error   ──► Serilog ES     ──► Log/App

Product-stack running ──► cadvisor ──► Prometheus ──► Ops/Infra overview (healthy/not)
Click identity        ──► same dashboard $service ──► detail metrics
catalogdb detail      ──► postgres-exporter (up / connections / size, no SQL text)

Collector / cadvisor / exporter / Grafana down ──► Catalog and Kafka keep running
```
