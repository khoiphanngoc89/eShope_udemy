# Quickstart: Paginated Catalog Browse and Shared Item Traits

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
- Data model: [data-model.md](./data-model.md)
- Grafana/Loki **must not** be required. Leave compose service `lgtm` stopped.

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
- Get-products returns one page, the filtered total, omits deleted/inactive products, and can filter by name, price, or category
- No Docker. No Grafana.

## Service-level checks

```bash
dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj --filter "FullyQualifiedName~EndpointTests"
```

Expected:

- `GET /products?pageNumber=1&pageSize=N` → 200, `items` length ≤ N, `totalCount` matches the filtered browse set
- `GET /products?name=bike`, `?price=10`, or `?category=outdoor` → 200 and only matching active, not-deleted products
- Empty catalog, no matches, or a page past the end → 200, `items` = `[]`
- `pageNumber=0`, `pageSize=101`, or `price=-1` → 400
- `GET /products/{id}` → 200 for a stored active, not-deleted product; 404 if missing, deleted, or inactive
- `GET /products/{id}` → 404 for missing or deleted ids
- Existing `POST /products` checks still pass (201, location, stored fields)

## Logging suite still green (FR-016)

```bash
dotnet test tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj
```

Expected: previously passing tests still pass. Do not edit that project in this feature.

## Full slice

```bash
dotnet test tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj
dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj
dotnet test tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj
```

Expected: all three succeed with Grafana stopped, in under 2 minutes for the isolated filter and under 5 seconds for a 25-product page on the service-level host (SC-001, SC-007).

## Out of scope for this validation

- `podman compose` / Grafana UI / Loki queries
- Public delete or activate/deactivate routes
- Search fields other than name, price, and category
- Changing Catalog Serilog or OTLP settings
