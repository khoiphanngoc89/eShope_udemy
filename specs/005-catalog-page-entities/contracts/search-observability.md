# Contract: Catalog search logs, metrics, and request traces

In-process signals for FR-021, FR-031, FR-032, FR-034. Isolated checks assert logs, meters, and in-process spans without Grafana, Tempo, Kafka UI, or Kibana.

## 1. Failed-request log (FR-021)

When `GET /products` or any Catalog request fails:

- Log includes request name, elapsed milliseconds, and the full stack in the message
- Public HTTP body MUST NOT include a stack
- Search-index outage uses this log plus HTTP 503 (see [get-products-api.md](./get-products-api.md))

## 2. Search metrics (FR-031)

Meter name: `eShop.Catalog.Search`. Exported on the existing OTLP path when LGTM is running.

| Instrument | Name | When |
|------------|------|------|
| Histogram | `catalog.search.duration` | Every product-page / name-price-category search attempt |
| Counter | `catalog.search.errors` | Incremented when that search fails (validation 400 may be excluded; index unavailable MUST increment) |

**Success of isolated checks**

- A successful in-memory page records duration
- A forced index failure increments `catalog.search.errors`

## 3. Sync lag and indexer failure (FR-032)

| Instrument | Name | When |
|------------|------|------|
| Observable gauge | `catalog.search.sync.lag_seconds` | How far the indexer is behind the latest `eshop.catalog.sync.public.mt_doc_product` offset. `0` when caught up or when the consumer is not running in isolated tests that do not start Kafka |

Indexer / CDC path failure writes an error log with exception (topic and product id when known). No success log per snapshot or streamed product.

## 4. Request trace in Grafana Tempo (FR-034)

When the log viewer stack (`lgtm`) is running and Catalog has handled at least one public request:

- Grafana **Explore → Tempo → Search** by service name `eShop.Catalog.Api` MUST show that request’s waterfall
- The waterfall is the Catalog HTTP request (ASP.NET instrumentation). A MediatR child span from source `eShop.Application` MAY appear under it
- An empty Tempo after traffic is a defect
- A Tempo **service graph** is not required

**Success of isolated checks**

- A listener on `Microsoft.AspNetCore` and/or `eShop.Application` records a span when a Catalog request or MediatR handle runs
- The isolated host does not start Grafana or Tempo

**Compose wiring that makes Tempo receive spans**

- Catalog compose MUST set `OTEL_EXPORTER_OTLP_ENDPOINT` to the in-stack LGTM OTLP HTTP URL (`http://lgtm:4318`), same collector as the Serilog log sink
- `AddAppOpenTelemetry` MUST listen to `applicationName` and `eShop.Application`
- Catalog MUST still start if LGTM is down

## Out of scope

- Starting Grafana, Tempo, Kafka UI, or Kibana for `dotnet test`
- Tempo service graph / span-metrics pipeline
- Per-product success logs
