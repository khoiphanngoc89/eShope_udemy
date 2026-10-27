# Implementation Plan: Catalog and BuildingBlocks Automated Tests

**Branch**: `003-catalog-buildingblocks-tests` | **Date**: 2026-10-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-catalog-buildingblocks-tests/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Add isolated (unit) and service-level (integration) automated checks for Catalog create-product and for the untested shared `BuildingBlock` (command handling and request-entry registration). Reuse the existing xUnit + `Microsoft.AspNetCore.Mvc.Testing` pattern from `BuildBlock.Logging.Tests`. Do not start Grafana/Loki. Do not rewrite logging tests. Do not add read/update/delete product APIs.

Technical approach: two new test projects. Catalog isolated checks call `CreateProductHandler` against an in-suite `IDocumentSession` substitute. Catalog service-level checks use `WebApplicationFactory` and `POST /products` against the same in-suite store (dummy `ConnectionStrings:CatalogDb` to pass startup validation; Marten session replaced so no live Postgres). BuildingBlock isolated checks dispatch a sample `ICommand` through MediatR and prove `AddAppCarter` registers request entry points. Minimal production seams: `public partial class Program` and `InternalsVisibleTo`.

## Technical Context

**Language/Version**: C# / .NET 10.0 (same as Catalog and existing test projects)

**Primary Dependencies**: xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, Microsoft.AspNetCore.Mvc.Testing 10.0.1, NSubstitute (test-only, `IDocumentSession`); existing MediatR, Carter, Marten packages already referenced by production projects

**Storage**: In-suite substitute for `IDocumentSession` (dictionary of stored `Product`s). No Testcontainers, no live Postgres, no operator `CatalogDb`. Dummy connection string only to satisfy `DatabaseOptions` ValidateOnStart on the happy-path host.

**Testing**: xUnit. Isolated checks in `tests/Services/eShop.Catalog.Api.Tests` and `tests/BuildingBlocks/BuildingBlock.Tests`. Service-level checks in the Catalog test project via `WebApplicationFactory<Program>`. Existing `tests/BuildingBlocks/BuildBlock.Logging.Tests` stays unchanged.

**Target Platform**: Local developer workstation and CI (`dotnet test`). No Grafana/Loki process.

**Project Type**: Test projects overlay on existing ASP.NET Catalog service + class-library building block

**Performance Goals**: Isolated Catalog + BuildingBlock suites under 2 minutes (SC-002). Catalog service-level suite under 5 minutes (SC-003).

**Constraints**: No change to Serilog/OTLP export or `deploy/observability/compose.yaml` (FR-009). No new Catalog product operations (FR-012). No new architectural layers. No duplicate logging tests (FR-008).

**Scale/Scope**: Two test projects, create-product only, one shared building block. Existing logging suite remains the third test project.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` is still the unratified placeholder. Gates below use that template’s example principles plus the workspace SDD / TDD / simplicity rules.

| Gate | Result | Evidence |
|------|--------|----------|
| Library-first | N/A | No new production library. Tests cover existing Catalog and `BuildingBlock`. |
| Test-first (non-negotiable) | PASS | Red: add failing tests for create-product and `BuildingBlock` before any production seam. Green: only `Program` partial + `InternalsVisibleTo` (and handler/DI only if a test proves a bug). |
| Integration / contract tests | PASS | `POST /products` contract in [contracts/create-product-api.md](./contracts/create-product-api.md). Service-level host hits that path. BuildingBlock public helpers contracted in [contracts/buildingblock-api.md](./contracts/buildingblock-api.md). |
| Observability | PASS | FR-009: do not change diagnostic export or Loki. Isolated checks run with Grafana down. |
| Simplicity / abstraction ban | PASS | No repository layer, no extra test framework wrapper. In-suite session substitute instead of Testcontainers. Reuse logging test package versions. |
| CLI interface | N/A | Operators run `dotnet test` (documented in quickstart). |
| Spec-first | PASS | Spec exists at `specs/003-catalog-buildingblocks-tests/spec.md`. |

**Post-design re-check**: PASS. Phase 1 adds Product/create-product/check-suite models, two HTTP/library contracts, and a `dotnet test` quickstart. That does not add a production layer. The only production files in scope are testability seams (`Program`, `InternalsVisibleTo`).

## Project Structure

### Documentation (this feature)

```text
specs/003-catalog-buildingblocks-tests/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── create-product-api.md
│   └── buildingblock-api.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
src/Services/eShop.Catalog.Api/
├── Program.cs                          # add public partial class Program (WebApplicationFactory)
└── eShop.Catalog.Api.csproj            # InternalsVisibleTo eShop.Catalog.Api.Tests

tests/Services/eShop.Catalog.Api.Tests/
├── eShop.Catalog.Api.Tests.csproj
├── Support/                            # factory, in-suite session substitute
├── CreateProductHandlerTests.cs        # isolated (US1)
└── CreateProductEndpointTests.cs       # service-level POST /products (US1, US3)

tests/BuildingBlocks/BuildingBlock.Tests/
├── BuildingBlock.Tests.csproj
├── CommandHandlingTests.cs             # isolated ICommand dispatch (US2)
└── AddAppCarterTests.cs                # request-entry registration (US2)

tests/BuildingBlocks/BuildBlock.Logging.Tests/   # unchanged (FR-008)

eShop.slnx                              # add the two test projects
```

**Structure Decision**: Mirror `tests/BuildingBlocks/BuildBlock.Logging.Tests`. Catalog tests live under `tests/Services/` next to the service. BuildingBlock tests live next to the logging tests. Do not put Catalog tests inside the logging project. Do not add `deploy/` or `src/BuildingBlocks/` production logic.

## Complexity Tracking

> No constitution violations to justify.
