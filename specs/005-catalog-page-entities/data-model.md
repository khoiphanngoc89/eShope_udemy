# Data Model: Paginated Catalog Browse, Shared Item Traits, and Search Sync

## BaseEntity (shared item base)

Owned by `BuildingBlock`. Catalog `Product` uses it. Requested name: `BaseEntity`.

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| Id | UUID | yes | Stable identity. Create assigns a new non-empty value. Update MUST NOT change it. Also the Elasticsearch document id. |

Implements the three traits below. Later services inherit this type instead of copying Catalog `Product`.

## Shared search store

Owned by `BuildingBlock` (`ISearchStore<T>`, `SearchQuery`, `SearchIndexer<T>`). Catalog maps product browse fields onto `SearchQuery` and uses index `catalog-products`. Other services reuse the same types with their own entity, index name, and Kafka topic.

## IAuditable

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| CreatedAt | date-time (UTC) | yes | Set on create. Update MUST NOT change it. |
| LastModified | date-time (UTC) | yes | Set on create (same instant as `CreatedAt`). Update MUST set a later value. Used as the search-document external version. |
| CreatedBy | string or null | no | Empty until Catalog has an authenticated caller. |
| LastModifiedBy | string or null | no | Empty until Catalog has an authenticated caller. |

## IDeletable

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| IsDeleted | boolean | yes | Default `false`. Default browse omits `true`. Get-by-id of `true` is not found. Indexer deletes the search document when this becomes `true`. |

## IActivable

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| IsActive | boolean | yes | Default `true` on create. Inactive products fail the base query. Indexer deletes the search document when this becomes `false`. |

## Product

Catalog item. Inherits `BaseEntity` (FR-009). Source of truth in the catalog store (Marten / PostgreSQL). Does **not** redefine `Id` or the trait fields.

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| Name | string | yes | Name search is case-insensitive contains. |
| Amount | integer | yes | Existing catalog field. |
| SewingCost | decimal | yes | Optional exact search field. Reject `< 0`. |
| FabricCostAccessoriesCost | decimal | yes | Optional exact search field. Reject `< 0`. |
| PackagingCost | decimal | yes | Optional exact search field. Reject `< 0`. |
| CostPrice | decimal | yes | Optional exact search field (`price` in the original spec). Reject `< 0`. |
| Description | string | yes | Returned on page items. |
| ImageFile | string | yes | Returned on page items. |
| Categories | list of string | yes | Category search is case-insensitive exact match against one entry. |

### State transitions

```text
Create → Active, not deleted, CreatedAt = LastModified = now
       → search document appears within 5s under normal sync (not in the create response)
Update → same Id, same CreatedAt, LastModified = now
       → search document updates within 5s under normal sync
Mark deleted (store only) → get-by-id = not found; search document removed
Mark inactive (store only) → get-by-id = not found; search document removed
Search index down → create/update/get-by-id still succeed; GET /products fails
```

### Persistence rules

- Isolated create: stored `Product` matches result id, product fields, and trait defaults (FR-010).
- Isolated update: stored `Product` keeps id and `CreatedAt`; `LastModified` is later (FR-011).
- Isolated / service-level get-by-id: load by id from `ISearchStore<Product>`; null, deleted, inactive, or store-only → not found. Index unavailable → 503.
- Isolated / service-level browse: read BuildingBlock `ISearchStore<Product>` only (FR-001). Seed `InMemorySearchStore<Product>` in tests.

## ProductSearchDocument

Read-model projection stored in Elasticsearch. Document id = `Product.Id`.

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| Id | UUID | yes | Same as `Product.Id`. |
| Name | string | yes | Analyzed for case-insensitive contains. |
| Amount | integer | yes | |
| SewingCost, FabricCostAccessoriesCost, PackagingCost, CostPrice | decimal | yes | Exact match filters. |
| Description | string | yes | Returned to the client. |
| ImageFile | string | yes | Returned to the client. |
| Categories | list of keyword | yes | Case-insensitive exact match. |
| CreatedAt, LastModified | date-time | yes | Sort fields. Default browse order is `LastModified` desc, then `Id` asc (current handler). |
| CreatedBy, LastModifiedBy | string or null | no | |
| IsDeleted | boolean | yes | Documents with `true` MUST NOT remain in the index. |
| IsActive | boolean | yes | Documents with `false` MUST NOT remain in the index. |
| Version | integer (external) | yes | `LastModified` UTC ticks (or Marten version). Stale or duplicate Kafka records MUST NOT overwrite a newer document. |

### Index query

- Index name (logical): `catalog-products`.
- Always exclude missing documents (deleted / inactive are not present).
- Apply optional name / cost / category filters AND.
- Page with `from` / `size`; `totalCount` is the filtered hit count.

## SearchSyncEvent

Debezium change event consumed by the indexer. Not a Catalog public API.

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| Topic | string | yes | MUST start with `eshop.catalog.sync`. Product table topic: `eshop.catalog.sync.public.mt_doc_product`. |
| Op | `c` / `r` / `u` / `d` | yes | `c`/`r`/`u` upsert when the after-image is active and not deleted; otherwise delete. `d` deletes. |
| After | Product JSON (Marten `data`) | no | Required for `c`/`r`/`u`. |
| Before | Product JSON | no | Used when `after` is missing on delete. |
| Source.ts_ms / LSN | integer | no | Optional lag input. |

### Search sync lifecycle

```text
Search sync starts → snapshot existing mt_doc_product (op=r) → stream WAL (op=c/u/d)
Create/update commit → Debezium emit → indexer upsert/delete → searchable ≤ 5s (normal sync)
Indexer failure → error log; write API unaffected
```

## PaginationRequest

Shared request shape in `BuildingBlock`.

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| PageNumber | integer | no | Default `1`. Catalog rejects `< 1`. |
| PageSize | integer | no | Default `10`. Catalog rejects `< 1` or `> 100`. |

## PaginatedResult&lt;T&gt;

Shared page shape in `BuildingBlock` (FR-012). Catalog browse uses `T = Product`.

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| PageNumber | integer | yes | Echoed from the request. |
| PageSize | integer | yes | Echoed from the request. |
| TotalCount | integer | yes | Count of the filtered browse set, not the page length. |
| Items | list of T | yes | One page. Empty list is success when the catalog is empty or the page is past the end. |

### Default browse set

- Include: active, not-deleted products that exist in the search index.
- Default order: `LastModified` descending, then `Id` ascending.
- `OrderBy` / `OrderDirection` may override using the supported product fields.
- Slice: page number and page size from the request.

## Product search (GET /products)

Optional query fields. Served from the search index.

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| Name | string or omitted | no | Case-insensitive contains. Blank or omitted = no name filter. |
| CostPrice | decimal or omitted | no | Exact match. Omitted = no filter. Reject `< 0`. |
| SewingCost | decimal or omitted | no | Exact match. Reject `< 0` when supplied. |
| FabricCostAccessoriesCost | decimal or omitted | no | Exact match. Reject `< 0` when supplied. |
| PackagingCost | decimal or omitted | no | Exact match. Reject `< 0` when supplied. |
| Category | string or omitted | no | Case-insensitive exact match against one listed category. |
| OrderBy | enum | no | Default `CreatedAt` on the public request (existing endpoint); index default order remains last-modified then id when the handler currently does that. Keep the live endpoint’s enum. |
| OrderDirection | enum | no | `asc` or `desc`. |

Combined filters AND. Index unavailable → service error, not an empty page.

## Search-sync topics

| Topic | Purpose |
|-------|---------|
| `eshop.catalog.sync.public.mt_doc_product` | Snapshot and stream of catalog products |
| `eshop.catalog.sync.connect-configs` | Kafka Connect config topic |
| `eshop.catalog.sync.connect-offsets` | Kafka Connect offsets |
| `eshop.catalog.sync.connect-status` | Kafka Connect status |
| `eshop.catalog.sync.schema-history` | Debezium schema history |

No catalog search-sync topic may omit the `eshop.catalog.sync` prefix.

## Observability entities

| Name | Kind | Rules |
|------|------|--------|
| Failed-request log | log | Request name, duration, stack in the message (FR-021). |
| `catalog.search.duration` | histogram | Search / page latency. |
| `catalog.search.errors` | counter | Search / page failures. |
| `catalog.search.sync.lag_seconds` | gauge | How far the indexer is behind the search-sync topic. |
| Sync/indexer failure log | log | Written on indexer or CDC path failure. No per-product success log. |
| Request trace | Tempo waterfall | One public Catalog request after LGTM is up. Service name `eShop.Catalog.Api`. Parent = ASP.NET HTTP span; child = `eShop.Application` MediatR span when the pipeline runs. Service graph is out of scope. |

## Isolated check vs service-level check

| Kind | Entry | Store | Search index | Log viewer / Kafka / ES |
|------|--------|--------|--------------|-------------------------|
| Isolated BuildingBlock | construct `BaseEntity` / `PaginatedResult<T>` | None | None | Must not be required |
| Isolated Catalog get-by-id | `GetByIdHandler.Handle` | In-suite session | Unused | Must not be required |
| Isolated Catalog browse | `GetProductsHandler.Handle` | Unused | `InMemorySearchStore<Product>` | Must not be required |
| Isolated indexer | apply Debezium-shaped event | Unused | In-memory store | Must not be required |
| Isolated Catalog update | `UpdateProductHandler.Handle` | In-suite session | Unused | Must not be required |
| Service-level get-by-id | `GET /products/{id}` | In-suite session | Unused | Must not be required |
| Service-level browse | `GET /products` | Unused | In-memory store behind the host | Must not be required |
| Service-level search outage | `GET /products` | Unused | Store configured to fail | Must not be required |
| Existing create-product | unchanged tests | In-suite session | Unused | Must not be required |
| Isolated request trace | handle a Catalog request with an `ActivityListener` | Unused / in-suite | Unused | Must not start Tempo |
| Compose freshness | create then wait ≤ 5s | Live catalogdb | Live Elasticsearch | Kafka + Debezium; Grafana not required |
| Compose operator UIs | open Kafka UI / Kibana / Tempo Explore | Live catalogdb | Live `catalog-products` | Kafka UI filters `eshop.catalog.sync*`; Kibana shows indexed products; Tempo Explore shows the Catalog request waterfall after traffic |
