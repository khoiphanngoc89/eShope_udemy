# Quickstart: Modern Grafana Log Collection

Validation guide for `007-loki-promtail`. Implementation steps belong in `tasks.md` (not created by `/speckit-plan`).

## Prerequisites

- .NET 10 SDK
- Existing suites:
  - [tests/Services/eShop.Catalog.Api.Tests](../../tests/Services/eShop.Catalog.Api.Tests)
  - [tests/BuildingBlocks/BuildingBlock.Tests](../../tests/BuildingBlocks/BuildingBlock.Tests)
  - [tests/BuildingBlocks/BuildBlock.Logging.Tests](../../tests/BuildingBlocks/BuildBlock.Logging.Tests)
- Contracts: [loki-explore.md](./contracts/loki-explore.md), [compose-collector.md](./contracts/compose-collector.md)
- Data model: [data-model.md](./data-model.md)

Grafana and the log collector **must not** be required for `dotnet test`.

## Isolated checks

```bash
dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj
dotnet test tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj
dotnet test tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj
```

Expected:

- Existing suites still pass
- Isolated compose/config checks (if added) do not start Docker
- No Grafana. No Alloy/Promtail

## Compose operator validation

```bash
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait \
  lgtm kafka catalogapi log-collector
```

Then:

1. Open Grafana **http://localhost:3000** (admin / admin). Explore → **Loki**. Do not add a data source.
2. Last 15 minutes: `{compose_service="kafka"}` — broker process lines after Kafka is healthy. Not topic JSON.
3. Call Catalog once (`GET http://localhost:5000/products` or create). `{compose_service="catalogapi"}` shows those lines.
4. Cause one Catalog failure (invalid page or missing id). Confirm the Error text in Loki within 2 minutes (`|= "Error"` or `|= "failed"`).
5. Confirm `{compose_service="kafka"}` does not show Catalog lines.
6. Confirm Kafka UI http://localhost:8080 still shows topic contents.
7. Confirm Ops/Infra and Log/App (006) still open.
8. Stop `log-collector`. `POST /products` and `GET /products/{id}` still succeed. Kafka still healthy.

## Out of scope for this validation

- Slack/email paging
- Production TLS
- Collecting Connect, Kafka UI, or databases
- Shipping Kafka topic payloads into Loki
- Removing OTLP (002) or Elasticsearch Log/App (006)
- Rewriting `BuildBlock.Logging.Tests`
