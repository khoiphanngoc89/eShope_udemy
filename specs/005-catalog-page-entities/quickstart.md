# Quickstart: Paginated Catalog Browse, Shared Item Traits, and Search Sync

Validation guide for `005-catalog-page-entities`. Implementation steps belong in `tasks.md` (not created by `/speckit-plan`).

## Prerequisites

- .NET 10 SDK
- Repo projects:
  - [src/BuildingBlocks/BuildingBlock/BuildingBlock.csproj](../../src/BuildingBlocks/BuildingBlock/BuildingBlock.csproj)
  - [src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj](../../src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj)
  - [tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj](../../tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj)
  - [tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj](../../tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj)
  - [tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj](../../tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj)
- Contracts:
  - [contracts/buildingblock-entities.md](./contracts/buildingblock-entities.md)
  - [contracts/get-products-api.md](./contracts/get-products-api.md)
  - [contracts/get-product-by-id-api.md](./contracts/get-product-by-id-api.md)
  - [contracts/search-sync.md](./contracts/search-sync.md)
  - [contracts/search-observability.md](./contracts/search-observability.md)
- Data model: [data-model.md](./data-model.md)
- Grafana/Loki **must not** be required for `dotnet test`. Leave compose service `lgtm` stopped for isolated and service-level checks.

## Isolated checks (under 2 minutes)

From the repo root:

```bash
dotnet test tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj
dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj --filter "FullyQualifiedName~HandlerTests"
```

Expected:

- `BaseEntity` exposes identity plus audit, deletion, and activable traits; a new instance is active and not deleted
- `PaginatedResult<T>` holds page number, page size, total count, and items
- Create-product stores trait defaults (`CreatedAt`, `LastModified`, `IsActive`, `IsDeleted`)
- Update-product keeps `Id` and `CreatedAt` and moves `LastModified` forward
- Get-by-id returns a stored, not-deleted product and reports not found for missing or deleted ids
- Get-products reads the in-memory search store, returns one page and the filtered total, omits deleted/inactive products, and can filter by name, cost, or category
- Get-products throws / maps to a service error when the search store is unavailable
- Indexer applies a Debezium-shaped create/update as an upsert and a delete / inactive / deleted event as a remove
- Search duration and error meters move; indexer failure is logged; no per-product success log is required
- A Catalog request or MediatR handle starts an in-process activity (`Microsoft.AspNetCore` and/or `eShop.Application`); Tempo is not started
- No Docker. No Grafana. No Tempo. No Kafka. No Elasticsearch. No Kafka UI. No Kibana.

## Service-level checks

```bash
dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj --filter "FullyQualifiedName~EndpointTests"
```

Expected:

- `GET /products?pageNumber=1&pageSize=N` → 200, `items` length ≤ N, `totalCount` matches the filtered browse set (from the in-memory search store)
- `GET /products?name=bike`, `?costPrice=10`, or `?category=outdoor` → 200 and only matching active, not-deleted products
- Empty catalog, no matches, or a page past the end → 200, `items` = `[]`
- `pageNumber=0`, `pageSize=101`, or `costPrice=-1` → 400
- Search store unavailable → `GET /products` is **503**; `GET /products/{id}` for a stored active product is still **200**
- `GET /products/{id}` → 200 for a stored active, not-deleted product; 404 if missing, deleted, or inactive
- Existing `POST /products` checks still pass (201, location, stored fields)

## Logging suite still green (FR-016)

```bash
dotnet test tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj
```

Expected: previously passing tests still pass. Do not edit that project in this feature.

## Full isolated + service-level slice

```bash
dotnet test tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj
dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj
dotnet test tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj
```

Expected: all three succeed with Grafana, Tempo, Kafka, Elasticsearch, Kafka UI, and Kibana stopped. Isolated filter under 2 minutes. A 25-product page on the service-level host under 5 seconds (SC-001, SC-007).

## Compose search-sync validation (operator)

Requires the local container runtime. Grafana (`lgtm`) is optional for search-sync freshness. Start `lgtm` for the Tempo check (SC-021).

```bash
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait catalogdb kafka connect elasticsearch kibana kafka-ui catalogapi
```

Then:

1. Create a product on the documented Catalog HTTP URL (`POST /products` on host port 5000).
2. Confirm the create response returns before the product is required in search.
3. Within 5 seconds, `GET /products?name=...` includes that product.
4. Open Kafka UI on the documented local URL and filter `eshop.catalog.sync`. Every catalog search-sync topic is under that prefix.
5. Open Kibana on **http://localhost:5601**, use data view `catalog-products`, and see the same product document.
6. Stop Elasticsearch. `GET /products` is 503. `GET /products/{id}` for the created product is still 200. Create still succeeds.

Tempo request-trace check (FR-034). Grafana can stay down for steps 1–6.

```bash
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait lgtm catalogapi
```

Then send one public Catalog request (`GET /products` or `POST /products`). Open Grafana at **http://localhost:3000** (admin / admin) → **Explore → Tempo → Search** → service name `eShop.Catalog.Api`. Expected: that request’s waterfall is present. A Tempo service graph is not required. Empty Tempo after traffic is a defect.

## Out of scope for this validation

- Public delete or activate/deactivate routes
- Rewriting Serilog or `BuildBlock.Logging.Tests`
- Requiring Grafana, Tempo, Kafka UI, or Kibana for `dotnet test`
- Tempo service graph
- Using the Debezium Elasticsearch sink
