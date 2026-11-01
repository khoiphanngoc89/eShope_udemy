# Quickstart: Hybrid Ops and App Observability

Validation guide for `006-hybrid-observability`. Implementation steps belong in `tasks.md` (not created by `/speckit-plan`).

## Prerequisites

- .NET 10 SDK
- Existing suites:
  - [tests/Services/eShop.Catalog.Api.Tests](../../tests/Services/eShop.Catalog.Api.Tests)
  - [tests/BuildingBlocks/BuildingBlock.Tests](../../tests/BuildingBlocks/BuildingBlock.Tests)
  - [tests/BuildingBlocks/BuildBlock.Logging.Tests](../../tests/BuildingBlocks/BuildBlock.Logging.Tests)
- Contracts: [ops-infra.md](./contracts/ops-infra.md), [log-app.md](./contracts/log-app.md), [compose-hybrid.md](./contracts/compose-hybrid.md)
- Data model: [data-model.md](./data-model.md)

Grafana, Elasticsearch, and the exporter **must not** be required for `dotnet test`.

## Isolated checks

```bash
dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj
dotnet test tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj
dotnet test tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj
```

Expected:

- Existing Catalog, BuildingBlock, and logging suites still pass
- Isolated Error-document shape check (if added) does not open a network socket
- No Docker. No Grafana. No Elasticsearch exporter

## Compose operator validation

```bash
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait \
  elasticsearch elasticsearch-exporter lgtm catalogapi
```

Then:

1. Open Grafana **http://localhost:3000** (admin / admin). Confirm Prometheus and Elasticsearch data sources exist. Do not add one.
2. Open **Ops/Infra**. Confirm cluster health, CPU, heap, shards, and document count. Zero documents must not show a cluster-down alert.
3. `curl -s http://localhost:9114/metrics` includes `elasticsearch_cluster_health_`.
4. Cause one Catalog failure (invalid page, or stop Elasticsearch and call `GET /products`). Confirm the Error appears on **Log/App** within 2 minutes (`service.name` = Catalog).
5. Confirm Loki Explore still finds the same service logs.
6. Stop `elasticsearch-exporter` and/or `lgtm`. `POST /products` and `GET /products/{id}` still succeed.

## Out of scope for this validation

- Slack/email paging
- Production TLS / ES auth
- Replacing Kibana `catalog-products` or Kafka UI
- Rewriting `BuildBlock.Logging.Tests`
- Tempo service graph
