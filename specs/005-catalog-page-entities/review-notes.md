# Review notes: GetProducts — how it works and how it was implemented

Date: 2026-11-01  
Spec: [005-catalog-page-entities](./spec.md)  
Context: local compose. `GET /products` reads Elasticsearch `catalog-products`, not Marten. Import / create / update write Marten only (no dual-write).

---

# Part A — How it works

## A1. Two stores, two read paths

```
Client
  │
  ├─ POST/PUT /products, POST /products/import, GET /products/{id}
  │     └─► catalogdb (Marten, mt_doc_product)     write + get-by-id
  │
  └─ GET /products  (page / name / price / category)
        └─► Elasticsearch catalog-products         browse / search only
```

Create, update, and import **do not** write Elasticsearch. Search is filled later by CDC.

## A2. How a new product reaches GetProducts

```
1. Catalog commits the product to Marten (mt_doc_product). HTTP returns.
2. If Kafka is configured, Catalog applies Marten schema on startup so the table exists.
3. connect-register POSTs deploy/debezium/catalog-product-connector.json
   (after Catalog is healthy). snapshot.mode=initial then streams WAL.
4. Debezium writes Kafka topics prefixed eshop.catalog.sync
   (table topic: eshop.catalog.sync.public.mt_doc_product).
5. Catalog SearchIndexer consumes that topic and upserts catalog-products.
6. GET /products reads Elasticsearch. Combined filters are AND.
```

Until step 5 finishes, get-by-id works and GetProducts may be empty. That is expected, not a failed import.

## A3. What GetProducts returns when search is empty or down

| Elasticsearch | Index `catalog-products` | `GET /products` |
|---------------|--------------------------|-----------------|
| Down / connection error | — | **503** `{ message }` (no stack) |
| Up | Missing (nothing synced yet) | **200** `items: []`, `totalCount: 0` |
| Up | Exists, no match for filters | **200** empty page |
| Up | Exists, matches | **200** page of active, not-deleted products |

Missing index ≠ outage. The cluster is live; CDC has not created the index yet.

## A4. Name search (including Vietnamese)

Spec: case-insensitive **contains on the whole name**, not tokenized words. No accent-folding.

| Query | Matches `Đầm Alice - size 1 (Xanh dương)`? |
|-------|--------------------------------------------|
| full name | yes (`name.keyword` + case-insensitive wildcard) |
| `Alice` / `Đầm` | yes (substring of the whole name) |
| `dam` | no (diacritics not folded) |

Analyzed `name` tokens (`đầm`, `alice`, …) are **not** used for contains. A wildcard on `text` cannot match a multi-word string as one value.

## A5. Mental model

- Marten = source of truth for writes and get-by-id.
- Elasticsearch = browse/search projection.
- Debezium + Kafka = the only sync path (no dual-write).
- Connector must be registered or the projection stays empty.
- Kafka UI = topic payloads. Kibana = `catalog-products` documents.

---

# Part B — How to implement (detail)

This is what we actually shipped for the import → GetProducts gap and Vietnamese name search. Follow this if you re-do or review the change.

## B0. Order (TDD)

```
1. Failing SearchStoreTests: missing index → empty page; ContainsQuery uses field.keyword
2. Failing GetProductsHandlerTests: Vietnamese full name
3. ElasticsearchSearchStore.IsMissingIndex + ContainsQuery
4. Schema-on-startup when Kafka is set
5. connect-register + connector JSON
6. compose --wait: register container must stay up (sleep + healthcheck)
7. README
```

Do **not** upsert Elasticsearch in create/import handlers.

## B1. Missing index → empty page

File: `src/BuildingBlocks/BuildingBlock/Search/ElasticsearchSearchStore.cs`

In `SearchAsync`, after `!response.IsValidResponse`:

```csharp
if (IsMissingIndex(type, reason))
    return new PaginatedResult<T>(query.PageNumber, query.PageSize, 0, []);

throw new SearchIndexUnavailableException(...); // real outage → 503
```

```csharp
internal static bool IsMissingIndex(string? type, string? reason) =>
    string.Equals(type, "index_not_found_exception", StringComparison.OrdinalIgnoreCase)
    || (!string.IsNullOrWhiteSpace(reason)
        && reason.Contains("no such index", StringComparison.OrdinalIgnoreCase));
```

Test: `tests/BuildingBlocks/BuildingBlock.Tests/SearchStoreTests.cs`.

## B2. Name contains on `name.keyword`

Same file. Do **not** wildcard the analyzed `name` field.

```csharp
internal static WildcardQuery ContainsQuery(string field, string value) => new()
{
    Field = $"{Camel(field)}.keyword",
    Value = $"*{value.Trim()}*",
    CaseInsensitive = true
};
```

Auto-mapping already created `name.keyword`. No reindex.

Tests: `SearchStoreTests` (query shape) + `GetProductsHandlerTests` (Vietnamese full name).

## B3. Marten table before Debezium registers

File: `src/Services/eShop.Catalog.Api/DependencyInjection.cs` (`UsePipline`)

When `Kafka:BootstrapServers` is set, apply schema **before** the app is healthy so `connect-register` can snapshot `public.mt_doc_product`:

```csharp
if (!string.IsNullOrWhiteSpace(app.Configuration[$"{KafkaOptions.SectionName}:BootstrapServers"]))
{
    app.Services.GetRequiredService<Marten.IDocumentStore>()
        .Storage.ApplyAllConfiguredChangesToDatabaseAsync()
        .GetAwaiter()
        .GetResult();
}
```

Compose already sets `Kafka__BootstrapServers=kafka:9092` on `catalogapi`. Isolated tests do not set Kafka → no schema apply against a dummy connection.

## B4. Auto-register the connector

File: `deploy/debezium/catalog-product-connector.json`

- Connector name: `eshop-catalog-product`
- Table: `public.mt_doc_product`
- `topic.prefix`: `eshop.catalog.sync`
- `snapshot.mode`: `initial`
- Slot: `eshop_catalog_sync`

`deploy/compose.yaml`: service `connect-register` (same Debezium image).

`deploy/compose.override.yaml`:

- `depends_on`: `catalogapi` healthy + `connect` started
- Mount the JSON read-only
- POST once if the connector is missing
- `exec sleep infinity` so the container does not exit 0 (compose `--wait` needs a running healthy task)
- Healthcheck: `curl` `http://connect:8083/connectors/eshop-catalog-product`

Do **not** make `catalogapi` wait on Connect or Elasticsearch.

## B5. Operator check

```bash
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait \
  kafka connect elasticsearch kibana kafka-ui connect-register

curl http://localhost:8083/connectors/eshop-catalog-product/status

# After snapshot (a few seconds)
curl -G "http://localhost:5000/products" --data-urlencode "name=Đầm Alice - size 1 (Xanh dương)"
```

Empty catalog, ES up, no index yet: `GET /products` → **200** empty `items`, not 503.

---

# Part C — Incidents that drove the two code fixes

## C1. After import, GetProducts returned `no such index [catalog-products]`

### Symptom

- `POST /products/import` succeeded.
- `GET /products` → 503 `{ "message": "no such index [catalog-products]" }`.
- Postgres had the rows (`mt_doc_product`, 47 products).
- Elasticsearch had no `catalog-products`.
- Connect had no connector (`GET http://localhost:8083/connectors` → `[]`).

### Why (before)

1. Import writes Marten only (no dual-write).
2. Search depends on Debezium → Kafka → indexer.
3. Connector registration was a **manual** README curl. It was never run.
4. ES does not create an index on search; the first upsert creates `catalog-products`.
5. `SearchAsync` treated **any** invalid ES response as 503, including `index_not_found_exception`.

### After

- `connect-register` POSTs the connector (`snapshot.mode=initial`). Snapshot exported the 47 rows; indexer created the index; `totalCount: 47`.
- Missing index → empty 200 page. Real cluster failures still 503.
- Schema apply when Kafka is set so the table exists before register.

### Files

| Change | File |
|--------|------|
| Missing index → empty page | `ElasticsearchSearchStore.cs` (`IsMissingIndex`) |
| Schema apply when Kafka is set | `DependencyInjection.cs` (`UsePipline`) |
| Auto-register connector | `deploy/compose.yaml`, `compose.override.yaml` (`connect-register`) |
| Connector config | `deploy/debezium/catalog-product-connector.json` |
| Tests | `tests/BuildingBlocks/BuildingBlock.Tests/SearchStoreTests.cs` |

## C2. Vietnamese full-name search returned zero hits

### Symptom

```http
GET /products?name=Đầm Alice - size 1 (Xanh dương)
```

`totalCount: 0` while the document existed. Tokens like `Lily` / `Alice` still matched.

### Why (before)

Wildcard on analyzed `name`:

```csharp
Field = "name", Value = $"*{value.ToLowerInvariant()}*"
```

Standard analyzer splits:

```text
Đầm Alice - size 1 (Xanh dương)
→ đầm, alice, size, 1, xanh, dương
```

Wildcard runs **per token**. The full name is not a token.

| Term | Old result |
|------|------------|
| `Lily` / `Alice` | match |
| `Đầm` | match (one token) |
| `dam` | no match |
| `ầm Li` | no match |
| full name | no match |

### After

`name.keyword` + `CaseInsensitive = true` (section B2). Same full Vietnamese name matches. `dam` still does not match `Đầm` (no accent-folding).

### Files

| Change | File |
|--------|------|
| Keyword contains | `ElasticsearchSearchStore.cs` (`ContainsQuery`) |
| Query-shape test | `SearchStoreTests.cs` |
| Handler test | `GetProductsHandlerTests.cs` |
