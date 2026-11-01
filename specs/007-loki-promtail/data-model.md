# Data Model: Modern Grafana Log Collection

No new Catalog public HTTP resources. Operator views over process logs already printed to stdout.

## CatalogProcessLogLine

One Loki stream entry collected from `eshop_catalog_api` stdout/stderr.

| Field / label | Required | Rules |
|---------------|----------|--------|
| `compose_service` | yes | `catalogapi`. Filter key vs Kafka. |
| `container` | no | `eshop_catalog_api` when set. |
| timestamp | yes | Line time (Docker / Loki). |
| line body | yes | Console text. Failed-request Errors include request name, duration, stack (existing 005). |
| severity | no | Present when the line includes Error/Fatal (or OTLP `detected_level` on the 002 path). |

### Lifecycle

```text
Catalog writes stdout → Docker json-file → Alloy keep catalogapi → Loki → Explore
Collector down → line stays on console only; Catalog process stays up
```

## KafkaBrokerProcessLogLine

One Loki stream entry collected from `eshop_kafka` stdout/stderr.

| Field / label | Required | Rules |
|---------------|----------|--------|
| `compose_service` | yes | `kafka`. Must not appear on Catalog-only queries. |
| `container` | no | `eshop_kafka` when set. |
| timestamp | yes | Line time. |
| line body | yes | Broker process text (startup, listeners, errors). **Not** a topic message body. |

### Lifecycle

```text
Kafka broker stdout → Docker json-file → Alloy keep kafka → Loki → Explore
Collector down → broker keeps running; use `docker logs eshop_kafka` or Kafka UI
```

## LogCollector

Compose sidecar. Not a Catalog type.

| Attribute | Rules |
|-----------|--------|
| Targets | Only `eshop_catalog_api` and `eshop_kafka` |
| Sink | Existing Loki in `lgtm` (`/loki/api/v1/push`) |
| Out of band | Connect, Kafka UI, DBs, topic consume |

## Relationships

```text
Catalog stdout ──► log-collector ──► Loki (lgtm) ──► Explore
Kafka stdout   ──►        │
                          └── does not read Kafka topics

Catalog Error  ──► Serilog ES sink (006) ──► Log/App   (unchanged)
OTLP logs      ──► lgtm :4318 ──► Loki                 (unchanged, overlap OK)
```
