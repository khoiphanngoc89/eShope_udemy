# Implementation Plan: Paginated Catalog Browse and Shared Item Traits

**Branch**: `005-catalog-page-entities` | **Date**: 2026-10-31 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-catalog-page-entities/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Add a public paginated product browse path, optional name/price/category search on that path, and put the shared item base plus page model in `BuildingBlock`. Catalog `Product` inherits `BaseEntity` (`IAuditable`, `IDeletable`, `IActivable`). Create and update keep their public paths and set audit / active / deleted defaults. Get-by-id treats a deleted product as not found. Isolated and service-level checks cover get-by-id, browse, and search; existing create-product and logging suites stay.

Technical approach: `PaginationRequest` / `PaginatedResult<T>` and the four entity contracts in `BuildingBlock`. `GET /products` uses `IQuery` + FluentValidation (page ≥ 1, size 1–100, price ≥ 0 when supplied) and standard LINQ on `ICatalogUnitOfWork.Products.FindAll()` (active, not-deleted, optional name contains / exact price / category match, `LastModified` desc, `Id` asc). Extend the existing in-suite session substitute so isolated browse does not need Postgres. Map `ValidationException` to HTTP 400 on the existing `WithException` helper; add `WithException` to get-by-id so missing/deleted returns 404.

## Technical Context

**Language/Version**: C# / .NET 10.0 (same as Catalog and existing test projects)

**Primary Dependencies**: Existing Catalog stack — MediatR, Carter, FluentValidation, Mapster, Marten 9. Test: xUnit, `Microsoft.AspNetCore.Mvc.Testing`, NSubstitute (already in Catalog tests)

**Storage**: Isolated checks and service-level host use the in-suite `IDocumentSession` substitute (dictionary of `Product`s, plus `Query<Product>()`). No Testcontainers, no live Postgres, no operator `CatalogDb`. Dummy connection string only to satisfy `DatabaseOptions` ValidateOnStart.

**Testing**: xUnit. Isolated BuildingBlock checks in `tests/BuildingBlocks/BuildingBlock.Tests`. Isolated and service-level Catalog checks in `tests/Services/eShop.Catalog.Api.Tests`. Existing `tests/BuildingBlocks/BuildBlock.Logging.Tests` stays unchanged.

**Target Platform**: Local developer workstation and CI (`dotnet test`). No Grafana/Loki process.

**Project Type**: ASP.NET Catalog service + existing class-library building block (no new project)

**Performance Goals**: Isolated Catalog + BuildingBlock suites under 2 minutes (SC-007). A 25-product page under 5 seconds on the service-level host (SC-001).

**Constraints**: No public delete or activate routes (FR-017). Optional name/price/category search is in scope on `GET /products` (FR-019, FR-020). No change to Serilog/OTLP or `deploy/` compose. No rewrite of create-product or logging tests (FR-016). Persistence reuses `IRepository<T>` / `ICatalogUnitOfWork`.

**Scale/Scope**: One new Catalog feature folder (`Products/GetProducts`), shared entity + pagination types in `BuildingBlock`, get-by-id/create/update handler seams, Catalog + BuildingBlock test files. Logging suite remains a third test project.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` is still the unratified placeholder. Gates below use that template’s example principles plus the workspace SDD / TDD / simplicity rules.

| Gate | Result | Evidence |
|------|--------|----------|
| Library-first | PASS | Shared item base and page model go into the existing `BuildingBlock` library (FR-008, FR-012). No new project. |
| Test-first (non-negotiable) | PASS | Red: BuildingBlock entity/page tests and Catalog get-by-id / get-products / update tests before production types and handlers. Green: smallest handler and type changes that pass. |
| Integration / contract tests | PASS | Browse and get-by-id contracts in [contracts/](./contracts/). Service-level host hits `GET /products` and `GET /products/{id}`. BuildingBlock types contracted in [contracts/buildingblock-entities.md](./contracts/buildingblock-entities.md). |
| Observability | PASS | Do not change diagnostic export or Loki. Isolated checks run with Grafana down (FR-015). |
| Simplicity / abstraction ban | PASS | Reuse `ICatalogUnitOfWork.Products`, `WithException`, `ValidationBehavior`, and the existing in-suite session substitute. Search is extra query parameters and LINQ `Where` on the existing browse handler, not a new search service. |
| CLI interface | N/A | Operators run `dotnet test` (documented in quickstart). |
| Spec-first | PASS | Spec exists at `specs/005-catalog-page-entities/spec.md`. |

**Post-design re-check**: PASS. Phase 1 adds entity/page models, two HTTP contracts, one library contract, and a `dotnet test` quickstart. Name/price/category search stays on `GET /products`. Create/update public paths stay. Delete/activate public paths stay out.

## Project Structure

### Documentation (this feature)

```text
specs/005-catalog-page-entities/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── get-products-api.md
│   ├── get-product-by-id-api.md
│   └── buildingblock-entities.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
src/BuildingBlocks/BuildingBlock/
├── Models/
│   ├── IAuditable.cs
│   ├── IDeletable.cs
│   ├── IActivable.cs
│   ├── BaseEntity.cs
│   ├── PaginationRequest.cs
│   └── PaginatedResult.cs
└── Extensions/EndpointExceptionExtension.cs   # map ValidationException → 400

src/Services/eShop.Catalog.Api/
├── Models/Product.cs                          # inherit BaseEntity; remove local Id
├── Products/CreateProduct/CreateProductHandler.cs
├── Products/UpdateProduct/UpdateProductHandler.cs
├── Products/GetById/GetByIdHandler.cs         # deleted → not found
├── Products/GetById/GetByIdEndpoint.cs        # WithException
└── Products/GetProducts/
    ├── GetProductsHandler.cs
    └── GetProductsEndpoint.cs

tests/BuildingBlocks/BuildingBlock.Tests/
├── BaseEntityTests.cs                         # US2 / US3 isolated
└── PaginatedResultTests.cs                    # US3 isolated

tests/Services/eShop.Catalog.Api.Tests/
├── Support/InMemoryDocumentSession.cs         # Query<Product>() stub
├── CreateProductHandlerTests.cs               # add trait assertions only
├── UpdateProductHandlerTests.cs               # US2 isolated
├── GetByIdHandlerTests.cs
├── GetByIdEndpointTests.cs
├── GetProductsHandlerTests.cs
└── GetProductsEndpointTests.cs

tests/BuildingBlocks/BuildBlock.Logging.Tests/ # unchanged (FR-016)
```

**Structure Decision**: Keep Catalog tests next to the service and BuildingBlock tests next to the existing BuildingBlock suite. Put requested models in `BuildingBlock/Models`. Add one Catalog feature folder for browse. Do not add `deploy/` work. Do not put Catalog tests inside the logging project.

## Complexity Tracking

> No constitution violations to justify.
