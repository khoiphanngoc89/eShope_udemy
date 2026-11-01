# Research: Paginated Catalog Browse, Shared Item Traits, and Search Sync

Prior entity/page decisions from 2026-10-31 stay. This pass adds the search-index and change-data-capture path required by Session 2026-11-01.

## 1. Shared item base lives in BuildingBlock, not Catalog

**Decision**: Keep public `IAuditable`, `IDeletable`, `IActivable`, and abstract `BaseEntity` under `src/BuildingBlocks/BuildingBlock/`. Catalog `Product` inherits `BaseEntity`. No second copy of identity or lifecycle fields in Catalog.

**Rationale**: FR-008, FR-009. Types already exist. Ponytail rung 1: reuse.

**Alternatives considered**:
- New entity library — extra project, not requested.
- Marten soft-delete package — extra dependency for a few fields.

## 2. Audit, deletion, and activable field names

**Decision**: Unchanged. `BaseEntity.Id` (`Guid`). `IAuditable`: `CreatedAt`, `LastModified` (`DateTimeOffset` UTC), nullable actor fields. `IDeletable.IsDeleted` default `false`. `IActivable.IsActive` default `true`.

**Rationale**: Already implemented and covered by isolated checks (SC-003, SC-004).

**Alternatives considered**: Rename `IActivable` — rejected; requested name.

## 3. Shared page model in BuildingBlock

**Decision**: Keep `PaginationRequest` (defaults 1 / 10) and `PaginatedResult<T>` in `BuildingBlock`. Catalog browse still returns `PaginatedResult<Product>`.

**Rationale**: FR-012. Isolated BuildingBlock checks already construct these types (FR-014).

**Alternatives considered**: Catalog-only page DTO — rejected by FR-012.

## 4. Browse and search read the search index, not Marten

**Decision**: `GetProductsHandler` queries BuildingBlock `ISearchStore<Product>` backed by Elasticsearch in the real host. It does **not** call `ICatalogUnitOfWork.Products.FindAll()` for page/search. Catalog maps `GetProductsQuery` onto shared `SearchQuery`. Query parameters stay as they are today (`name`, `costPrice`, `sewingCost`, `fabricCostAccessoriesCost`, `packagingCost`, `category`, `orderBy`, `orderDirection`). The store applies the default browse set (`IsActive` and not `IsDeleted`) plus the same AND filters and documented order. Isolated and service-level hosts use `InMemorySearchStore<Product>` so `dotnet test` does not need Elasticsearch (FR-015, SC-007). Later services reuse the same BuildingBlock types with their own entity, index name, and topic.

**Rationale**: FR-001, FR-019, FR-023, FR-024. Session 2026-11-01: the search index is the browse/search read model. The current Marten LINQ page is the thing this pass replaces.

**Alternatives considered**:
- Keep Marten LINQ for `GET /products` — rejected by FR-001 / FR-019 after clarification.
- Fall back to Marten when Elasticsearch is down — rejected by FR-023.
- Testcontainers Elasticsearch for every `dotnet test` — conflicts with SC-007 and the existing in-suite host.

## 5. Search-index outage is a visible service error

**Decision**: When `ISearchStore<T>` cannot talk to the index, throw BuildingBlock `SearchIndexUnavailableException` (`ServiceUnavailableException`). `WithException` maps that type to HTTP **503** with `{ message }` and no stack. Keep FR-021 failed-request logging (name, duration, stack in the log). Get-by-id, create, and update do not use the search store and stay available.

**Rationale**: FR-023, FR-024, SC-011, SC-012. Empty 200 would look like an empty catalog. Silent Marten fallback hides the outage and can return a different match set.

**Alternatives considered**:
- HTTP 500 only — less accurate; the service is up, the search dependency is not.
- HTTP 200 empty page — rejected by FR-023.
- Cached last page — extra store, not requested.

## 6. Get-by-id stays on the catalog store

**Decision**: `GetByIdHandler` keeps using Marten through `ICatalogUnitOfWork`. Deleted or inactive remains not found. No Elasticsearch lookup on this path.

**Rationale**: FR-006, FR-007, FR-024. Point reads stay on the source of truth.

**Alternatives considered**: Serve get-by-id from Elasticsearch — rejected; index lag could hide a just-written product.

## 7. Sync is Debezium → Kafka → .NET indexer (no dual-write)

**Decision**: Production and local compose use PostgreSQL logical decoding. Debezium PostgreSQL connector snapshots `public.mt_doc_product` (Marten’s Product document table) then streams changes. Kafka topics for this connector all start with `eshop.catalog.sync`. A `BackgroundService` in `eShop.Catalog.Api` consumes those topics and upserts or deletes Elasticsearch documents. Create-product and update-product do **not** write Elasticsearch. The Debezium Elasticsearch sink is not used.

**Rationale**: FR-025–FR-028. Debezium’s Elasticsearch sink is still incubating and is not a production recommendation. A hosted indexer in Catalog reuses the existing service (no new .csproj). Document id = product `Id`. Treat delete / `IsDeleted` / `IsActive = false` as an index delete so browse never needs a second filter source. Use `LastModified` ticks (or Marten version when present) as an external version so at-least-once Kafka redelivery does not rewind a document.

**Alternatives considered**:
- App dual-write to Marten and Elasticsearch — rejected by FR-027; partial failure diverges.
- Wait in create/update until the product is searchable — rejected by FR-025; also breaks writes when search is down.
- Debezium Elasticsearch sink — incubating, not production-grade.
- New Catalog.Search worker project — extra project; not required by the spec.

## 8. Snapshot then stream

**Decision**: Connector `snapshot.mode=initial`. First start snapshots existing `mt_doc_product` rows, then streams WAL changes. Replay or a new slot repeats snapshot only when Debezium has no stored offset.

**Rationale**: FR-028, SC-015. Stream-only startup would hide products stored before sync.

**Alternatives considered**:
- `snapshot.mode=never` — rejected by FR-028.
- Manual reindex only — rejected; operators would have to run a job before search works.

## 9. Topic prefix, Kafka UI, and Kibana

**Decision**: Set Debezium `topic.prefix=eshop.catalog.sync`. Name Kafka Connect internal topics with the same prefix (`eshop.catalog.sync.connect-configs`, `eshop.catalog.sync.connect-offsets`, `eshop.catalog.sync.connect-status`, `eshop.catalog.sync.schema-history`). The product change topic is `eshop.catalog.sync.public.mt_doc_product`. Add `provectuslabs/kafka-ui` and official `kibana` (same major as Elasticsearch 8) to the existing `deploy/compose.yaml` pair. Kafka UI is published on a documented local HTTP port and pointed at the in-stack Kafka bootstrap. Kibana is published on documented local HTTP port **5601** and pointed at in-stack `http://elasticsearch:9200`. Local Kafka UI and Kibana have no auth (same class of local-dev default as catalogdb `catalog` / `catalog`). Catalog.Api and `dotnet test` MUST NOT require either UI.

**Rationale**: FR-029, FR-030, FR-033, SC-016, SC-020. Kafka UI traces prefixed topics. Kibana inspects `catalog-products` documents after snapshot or stream.

**Alternatives considered**:
- Default Debezium server name without a shared prefix — rejected by FR-029.
- kafbat/ui — same job; the request named kafka-ui.
- Elasticsearch only, no Kibana — rejected; the operator asked for Kibana UI.
- OpenSearch Dashboards — rejected; the search index is Elasticsearch.

## 10. Compose stack for search sync

**Decision**: Extend the existing compose pair (do not add a second compose file). New services: Kafka (KRaft, no ZooKeeper), Debezium Kafka Connect, Elasticsearch 8 single-node with security off for local-dev, Kafka UI, Kibana. Enable `wal_level=logical` on `catalogdb`. Catalog.Api **must not** `depends_on` Elasticsearch, Kafka, Kafka UI, or Kibana for process start: writes and get-by-id stay up if those are down (FR-024). Documented search-sync start waits for catalogdb, kafka, connect, and elasticsearch. Kibana waits for elasticsearch. Catalog.Api config gains `Elasticsearch__Uri` and `Kafka__BootstrapServers` (in-stack hosts vs published host ports, same pattern as CatalogDb).

**Rationale**: Feature 004 already owns one compose pair. Operators start search the same way they start the catalog store. Isolated `dotnet test` still does not start this stack (FR-015).

**Alternatives considered**:
- Separate `compose.search.yaml` — extra run path; prefix tracing is easier in the same network.
- Require Elasticsearch before Catalog starts — rejected by FR-024.

## 11. Observability: reuse failed-request logs; add meters

**Decision**: Keep `LoggingAndTelemetryBehavior` as the failed-request log (FR-021). Add OpenTelemetry **metrics** on the existing `AddAppOpenTelemetry` path (`WithMetrics` + OTLP exporter + one meter `eShop.Catalog.Search`):

| Instrument | Name | Meaning |
|------------|------|---------|
| Histogram | `catalog.search.duration` | Product-page / name-price-category search latency |
| Counter | `catalog.search.errors` | Failed search requests (including index unavailable) |
| Observable gauge | `catalog.search.sync.lag_seconds` | Consumer lag behind the latest search-sync topic offset (0 when caught up) |

Indexer / CDC failures use `ILogger` error with exception (topic, product id when known, stack). Do not log a success line per snapshot or streamed product (FR-032). Isolated checks assert meters and log messages in-process; they do not start Grafana.

**Rationale**: FR-031, FR-032, SC-017–SC-019. Logs already reach Loki through the Serilog OTLP sink. Metrics ride `AddOtlpExporter`. Traces were assumed present but were not reaching Tempo (see §14).

**Alternatives considered**:
- Per-product success logs — rejected by FR-032 (noise).
- New telemetry stack — rejected; LGTM already receives OTLP.
- Search API logs only — rejected; operators could not see sync lag.

## 12. Automated checks layout

**Decision**: Reuse the existing test projects. Isolated BuildingBlock checks cover `ISearchStore<T>`, `SearchQuery`, and `SearchIndexer<T>`. Isolated GetProducts tests seed `InMemorySearchStore<Product>`. Isolated Catalog indexer tests feed a Debezium-shaped payload into the shared indexer. Isolated metrics tests increment the Catalog meter. Isolated trace checks listen in-process for an ASP.NET or `eShop.Application` span (no Tempo). Service-level `GET /products` uses the in-memory store behind `WebApplicationFactory` and asserts 200 / 400 / 503. Service-level get-by-id and create stay on the in-suite Marten substitute. Existing create-product and logging suites stay. Compose quickstart proves snapshot + 5-second stream, Kafka UI, Kibana, and Grafana Tempo Explore after one Catalog request; `dotnet test` does not start Kafka, Elasticsearch, Kafka UI, Kibana, or Grafana.

**Rationale**: FR-013–FR-016, SC-006–SC-008, SC-014. Five-second freshness is an operator/compose proof plus isolated indexer mapping; the in-suite host cannot run real Debezium without breaking SC-007.

**Alternatives considered**:
- Testcontainers for Kafka + Elasticsearch on every CI test — slow, extra daemon, conflicts with the 2-minute isolated budget.
- Dual-write only in tests — would hide a missing consumer.

## 13. What not to touch

**Decision**: Do not change `POST /products` or `PUT /products/{id}` public bodies or routes. Do not add public delete or activate routes. Do not rewrite `BuildBlock.Logging.Tests` or replace create-product tests. Do not move `Product` out of Catalog. Do not use the Debezium Elasticsearch sink. Do not make Catalog startup require Grafana, Tempo, Kafka, Elasticsearch, Kafka UI, or Kibana. Do not add a Tempo service-graph / span-metrics pipeline.

**Rationale**: FR-016, FR-017, FR-024, FR-027, FR-034.

## 14. Grafana Tempo shows a Catalog request-trace waterfall

**Decision**: Empty Tempo after Catalog traffic is a defect (FR-034, SC-021). Keep the existing LGTM container. Do two small wiring fixes so traces actually arrive and nest under the HTTP request:

1. Set Catalog compose `OTEL_EXPORTER_OTLP_ENDPOINT=http://lgtm:4318` (and HTTP/protobuf if required) so `AddOtlpExporter()` for traces and metrics uses the same in-stack collector as the Serilog log sink. Today only `Serilog__WriteTo__1__Args__endpoint` points at LGTM; the OpenTelemetry exporter defaults to `http://localhost:4317` inside the Catalog container, so Tempo stays empty while Loki still shows logs.
2. In `AddAppOpenTelemetry`, `AddSource("eShop.Application")` in addition to `AddSource(applicationName)`. `LoggingAndTelemetryBehavior` already starts a MediatR activity on that source; without it the child span is dropped even after the endpoint is fixed. ASP.NET Core instrumentation already creates the parent HTTP span.

Operator check: with `lgtm` and `catalogapi` up, send one public Catalog request, then Grafana **Explore → Tempo → Search** by service name `eShop.Catalog.Api`. The waterfall MUST show that request. A Tempo **service graph** is out of scope. Isolated checks use an `ActivityListener`; they MUST NOT start Grafana or Tempo. Catalog MUST still start if LGTM is down (same fail-open rule as logs).

**Rationale**: FR-034, SC-021. Session 2026-11-01: Tempo Explore waterfall after traffic; not a service graph. Ponytail: reuse LGTM and the existing `WithTracing` path; do not add Alloy, Jaeger, or a second collector.

**Alternatives considered**:
- Tempo service graph — rejected by clarification; needs span metrics / extra Tempo config.
- Logs only (README “Tempo can be empty”) — rejected by FR-034.
- New tracing library or Jaeger — rejected; LGTM already includes Tempo.
- Start Tempo for `dotnet test` — rejected by FR-015 / FR-034.
