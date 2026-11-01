# Implementation Plan: Paginated Catalog Browse, Shared Item Traits, and Search Sync

**Branch**: `005-catalog-page-entities` | **Date**: 2026-11-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-catalog-page-entities/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Keep the shared item base, page model, and Elasticsearch search store in `BuildingBlock`. Catalog `Product` stays on `BaseEntity` and uses `ISearchStore<Product>`. Get-by-id, create, and update stay on the Marten catalog store. Change `GET /products` so browse and name/cost/category search read Elasticsearch. Sync the index with production-grade CDC: Debezium snapshots then streams `mt_doc_product` onto Kafka topics prefixed `eshop.catalog.sync`; a shared hosted indexer upserts or deletes Elasticsearch documents. Writes do not wait for search and do not dual-write the index. Search-index outage returns HTTP 503; get-by-id still works. Operator UIs: Kafka UI for prefixed topics, Kibana on port 5601 for `catalog-products`, Grafana Tempo Explore for a Catalog request-trace waterfall after traffic (no Tempo service graph). Metrics: search latency, search errors, sync lag, plus existing failed-request logs and indexer failure logs.

Technical approach: BuildingBlock `ISearchStore<T>` (Elasticsearch in compose, in-memory in tests), `SearchIndexUnavailableException` → 503, OpenTelemetry traces + metrics on the existing OTLP path (same LGTM endpoint as logs), Debezium + Kafka + Elasticsearch + Kafka UI + Kibana in the existing compose pair, `wal_level=logical` on `catalogdb`. No new .csproj. Isolated `dotnet test` does not start Kafka, Elasticsearch, Kafka UI, Kibana, or Grafana.

## Technical Context

**Language/Version**: C# / .NET 10.0 (same as Catalog and existing test projects)

**Primary Dependencies**: Existing Catalog stack — MediatR, Carter, FluentValidation, Mapster, Marten 9. New: `Elastic.Clients.Elasticsearch`, `Confluent.Kafka`. Compose: PostgreSQL 16 (logical decoding), Apache Kafka KRaft, Debezium Connect, Elasticsearch 8, `provectuslabs/kafka-ui`, official Kibana 8. Test: xUnit, `Microsoft.AspNetCore.Mvc.Testing`, NSubstitute

**Storage**: Catalog store = Marten on PostgreSQL (`catalogdb`). Search index = Elasticsearch (`catalog-products`). Sync log = Kafka topics `eshop.catalog.sync*`. Isolated / service-level `dotnet test` = in-suite Marten substitute + `InMemorySearchStore<Product>`. No Testcontainers.

**Testing**: xUnit. Isolated BuildingBlock checks in `tests/BuildingBlocks/BuildingBlock.Tests`. Isolated and service-level Catalog checks in `tests/Services/eShop.Catalog.Api.Tests`. Existing `tests/BuildingBlocks/BuildBlock.Logging.Tests` stays unchanged. Isolated trace checks listen in-process (no Tempo). Compose quickstart proves 5-second freshness, Kafka UI, Kibana, and Grafana Tempo Explore after one Catalog request.

**Target Platform**: Local developer workstation and CI (`dotnet test`). Optional Docker Desktop compose for search-sync and operator UIs. No Grafana/Loki process for automated checks.

**Project Type**: ASP.NET Catalog service + existing class-library building block + existing compose pair (no new project)

**Performance Goals**: Isolated Catalog + BuildingBlock suites under 2 minutes (SC-007). A 25-product page under 5 seconds on the service-level host (SC-001). Under normal compose sync, a committed write is searchable within 5 seconds (SC-014).

**Constraints**: No public delete or activate routes (FR-017). No dual-write of Elasticsearch on create/update (FR-027). Writes and get-by-id remain available when search, Kafka, Kafka UI, Kibana, or Tempo are down (FR-024, FR-033, FR-034). All search-sync topics use prefix `eshop.catalog.sync` (FR-029). No rewrite of create-product or logging tests (FR-016). Catalog startup must not require Grafana, Tempo, Kafka, Elasticsearch, Kafka UI, or Kibana. No Tempo service graph.

**Scale/Scope**: One Catalog search store + hosted indexer, compose services for Kafka / Connect / Elasticsearch / Kafka UI / Kibana, OpenTelemetry meters and request traces to existing LGTM Tempo, get-products handler read-source change. Logging suite remains a third test project.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` is still the unratified placeholder. Gates below use that template’s example principles plus the workspace SDD / TDD / simplicity rules.

| Gate | Result | Evidence |
|------|--------|----------|
| Library-first | PASS | Shared item base, page model, and Elasticsearch search store (`ISearchStore<T>`, `SearchQuery`, indexer) stay in `BuildingBlock`. No new project. |
| Test-first (non-negotiable) | PASS | Red: search-store, indexer, 503, and meter tests before the Elasticsearch client and Kafka consumer. Green: smallest handler swap and hosted service that pass. |
| Integration / contract tests | PASS | Browse, get-by-id, search-sync, and observability contracts in [contracts/](./contracts/). Service-level host hits `GET /products` and `GET /products/{id}`. Compose quickstart hits Kafka UI, Kibana, and Grafana Tempo Explore. |
| Observability | PASS | Reuse failed-request logs and OTLP. Add search duration, search errors, sync lag, indexer failure logs, and Catalog request traces in Grafana Tempo Explore. Isolated checks run with Grafana, Tempo, Kafka UI, and Kibana down. |
| Simplicity / abstraction ban | PASS | One generic `ISearchStore<T>`, one hosted indexer, existing compose pair. No Search microservice. Debezium ES sink rejected (incubating). Dual-write rejected by spec. |
| CLI interface | N/A | Operators run `dotnet test` and documented `docker compose` commands (quickstart). |
| Spec-first | PASS | Spec exists at `specs/005-catalog-page-entities/spec.md`. |

**Post-design re-check**: PASS. Phase 1 adds the search document, sync event, topic list, 503 browse contract, observability contract (logs, meters, Tempo request-trace waterfall), Kafka UI + Kibana operator contract, and a `dotnet test` + compose quickstart. Create/update public paths stay. Delete/activate public paths stay out. Catalog still starts if search UIs or Tempo are down. Tempo service graph stays out.

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
│   ├── buildingblock-entities.md
│   ├── search-sync.md
│   └── search-observability.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
src/BuildingBlocks/BuildingBlock/
├── Models/                                      # existing BaseEntity + page types
├── Search/                                      # ISearchStore<T>, SearchQuery, ES/in-memory stores, indexer
├── Extensions/EndpointExceptionExtension.cs     # add 503 for search-index unavailable
├── Extensions/SearchServiceCollectionExtensions.cs
└── Extensions/OpenTelemetryExtensions.cs        # traces + meters; listen to eShop.Application

src/Services/eShop.Catalog.Api/
├── Models/Product.cs
├── Products/GetById/                            # unchanged store path
├── Products/CreateProduct/                      # no ES write
├── Products/UpdateProduct/                      # no ES write
├── Products/GetProducts/
│   ├── GetProductsHandler.cs                    # map GetProductsQuery → SearchQuery
│   └── GetProductsEndpoint.cs                   # produce 503
└── Search/CatalogSearchMetrics.cs               # Catalog meter names only

deploy/
├── compose.yaml                                 # add kafka, connect, elasticsearch, kafka-ui, kibana
└── compose.override.yaml                        # ports, wal_level=logical, local-dev URLs

tests/BuildingBlocks/BuildingBlock.Tests/
└── SearchStoreTests.cs                          # shared store + indexer

tests/Services/eShop.Catalog.Api.Tests/
├── GetProductsHandlerTests.cs                   # index-backed browse + outage
├── GetProductsEndpointTests.cs                  # 200 / 400 / 503
├── ProductSearchIndexerTests.cs                 # Debezium payload → upsert/delete
└── CatalogSearchMetricsTests.cs

tests/BuildingBlocks/BuildBlock.Logging.Tests/   # unchanged (FR-016)
```

**Structure Decision**: Keep reusable Elasticsearch search types in `BuildingBlock.Search`. Catalog keeps Product query mapping, index name `catalog-products`, topic `eshop.catalog.sync.public.mt_doc_product`, and Catalog meter names. Point Catalog compose OTLP traces at the same LGTM endpoint as logs. Extend the existing compose pair for Kafka, Connect, Elasticsearch, Kafka UI, and Kibana. Do not put Catalog tests inside the logging project. Do not add a second compose file. Do not add a Tempo service-graph pipeline.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Kafka + Debezium + Elasticsearch + Kafka UI + Kibana in compose | Spec requires production-grade CDC search, prefixed topics, Kafka UI, and Kibana | Keep Marten LINQ on `GET /products` — rejected by FR-001 / FR-027 after clarification |
| `ISearchStore<T>` seam | Browse must be testable without Elasticsearch (FR-015, SC-007) | Live ES in every `dotnet test` — breaks the 2-minute isolated budget |
