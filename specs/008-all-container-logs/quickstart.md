# Quickstart: All Container Logs Without App Duplicates

Validation guide for `008-all-container-logs`. Implementation steps belong in `tasks.md` (not created by `/speckit-plan`).

## Prerequisites

- .NET 10 SDK
- Existing suites:
  - [tests/Services/eShop.Catalog.Api.Tests](../../tests/Services/eShop.Catalog.Api.Tests)
  - [tests/BuildingBlocks/BuildingBlock.Tests](../../tests/BuildingBlocks/BuildingBlock.Tests)
  - [tests/BuildingBlocks/BuildBlock.Logging.Tests](../../tests/BuildingBlocks/BuildBlock.Logging.Tests)
- Contracts: [compose-collector.md](./contracts/compose-collector.md), [loki-explore.md](./contracts/loki-explore.md), [ops-infra-overview.md](./contracts/ops-infra-overview.md)
- Data model: [data-model.md](./data-model.md)

Grafana, the log collector, cadvisor, and the store exporter **must not** be required for `dotnet test`.

## Isolated checks

```bash
dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj
dotnet test tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj
dotnet test tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj
```

Expected:

- Existing suites still pass
- Isolated compose/config checks (Alloy drop-list, Prometheus `catalogdb` + `containers` jobs, Ops/Infra inventory + `service` variable + data links, `catalogapi.depends_on` still `catalogdb` only) do not start Docker
- No Grafana. No Alloy. No cadvisor. No postgres-exporter

## Compose operator validation

```bash
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait \
  catalogdb postgres-exporter cadvisor lgtm kafka connect elasticsearch kibana kafka-ui \
  elasticsearch-exporter kafka-exporter catalogapi log-collector
```

Then:

1. Open Grafana **http://localhost:3000** (admin / admin). Explore → **Loki**. Do not add a data source.
2. Last 15 minutes: `{compose_service="catalogdb"}`, `{compose_service="kafka"}`, `{compose_service="connect"}`, `{compose_service="elasticsearch"}`, `{compose_service="kibana"}`, `{compose_service="kafka-ui"}`. Process lines appear after each container is healthy. Empty before first write is not a config error.
3. `{compose_service="kafka"}` is broker process text, not topic JSON. Kafka UI http://localhost:8080 still shows payloads.
4. Call Catalog once. `{service_name=~".*eShop.Catalog.Api.*"}` shows those application lines. `{compose_service="catalogapi"}` does **not** list the same request a second time.
5. Open **Ops/Infra**. Overview lists the product stack (store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, exporters) as healthy or not. Collector and Grafana need not appear.
6. Click **catalogdb**. Same dashboard shows CPU, RAM, store up, connections, and size. A small/zero size is not an outage. Click **catalogapi** (or elasticsearch / kafka) and confirm CPU, RAM, and existing richer detail. Click **kibana** and confirm CPU and RAM.
7. Optional: stop `kibana` and refresh the overview — Kibana is not healthy, not a Grafana config error.
8. Stop `log-collector`, `cadvisor`, and `postgres-exporter`. `POST /products` and `GET /products/{id}` still succeed. Isolated `dotnet test` still passes without starting them.

## Out of scope for this validation

- Slack/email paging
- Production TLS
- Shipping Kafka topic payloads into Loki
- Collecting `log-collector` or `lgtm` process output
- Requiring collector or Grafana on the health overview
- Removing OTLP (002), Elasticsearch Log/App (006), or Kafka UI
- Rewriting `BuildBlock.Logging.Tests`
