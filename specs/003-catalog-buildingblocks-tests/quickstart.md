# Quickstart: Catalog and BuildingBlocks Automated Tests

Validation guide for `003-catalog-buildingblocks-tests`. Implementation steps belong in `tasks.md` (not created by `/speckit-plan`).

## Prerequisites

- .NET 10 SDK
- Repo projects:
  - [src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj](../../src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj)
  - [src/BuildingBlocks/BuildingBlock/BuildingBlock.csproj](../../src/BuildingBlocks/BuildingBlock/BuildingBlock.csproj)
  - [tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj](../../tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj)
- Contracts:
  - [contracts/create-product-api.md](./contracts/create-product-api.md)
  - [contracts/buildingblock-api.md](./contracts/buildingblock-api.md)
- Grafana/Loki **must not** be required. Leave `deploy/observability` stopped.

## Isolated checks (under 2 minutes)

From the repo root:

```bash
dotnet test tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj
dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj --filter FullyQualifiedName~CreateProductHandlerTests
```

Expected: all pass. No Docker. No Grafana. Create-product handler stores name, price, description, image file, categories and returns a new id.

## Service-level checks (under 5 minutes)

```bash
dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj --filter FullyQualifiedName~CreateProductEndpointTests
```

Expected:

- `POST /products` → 201, `Location` `/products/{id}`, body `id`
- In-suite store has that product
- Second POST returns a different `id`
- Host without `ConnectionStrings:CatalogDb` fails with a message containing `ConnectionStrings:CatalogDb`

## Logging suite still green (FR-008)

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

Expected: all three succeed with Grafana stopped.

## Out of scope for this validation

- `podman compose` / Grafana UI / Loki queries
- Adding GET/UPDATE/DELETE product APIs
- Changing Catalog Serilog or OTLP settings
