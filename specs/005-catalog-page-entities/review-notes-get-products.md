# Review notes: GetProducts after import and Vietnamese name search

Date: 2026-11-01  
Context: local compose stack. `GET /products` reads Elasticsearch `catalog-products`, not Marten. Import / create / update write Marten only (no dual-write).

---

## 1. After import, GetProducts returned `no such index [catalog-products]`

### Symptom

- Used `POST /products/import`.
- `GET /products` returned 503:

```json
{ "message": "no such index [catalog-products]" }
```

- Postgres had the imported rows (`public.mt_doc_product`, 47 products).
- Elasticsearch had no `catalog-products` index.
- Kafka Connect had no connector (`GET http://localhost:8083/connectors` → `[]`).

### Why (before)

1. Import commits to Marten only. Spec forbids writing Elasticsearch on the write path.
2. Search is filled by CDC: Debezium snapshots/streams `mt_doc_product` → Kafka `eshop.catalog.sync.public.mt_doc_product` → Catalog indexer upserts Elasticsearch.
3. Connector registration was a **manual** curl in README. It was never run, so snapshot/stream never started.
4. Elasticsearch does not create an index on search. The first document creates `catalog-products`.
5. `ElasticsearchSearchStore.SearchAsync` treated **any** invalid ES response as `SearchIndexUnavailableException` (HTTP 503). `index_not_found_exception` is not an outage — the cluster is up, the index has not been created yet.

### After

- Registered connector `eshop-catalog-product` (`snapshot.mode=initial`). Snapshot exported all 47 rows; indexer created `catalog-products`; `GET /products` returned `totalCount: 47`.
- Missing index on search is now an empty page (`items: []`, `totalCount: 0`), not 503. Real connection/cluster failures still 503.
- Compose service `connect-register` POSTs `deploy/debezium/catalog-product-connector.json` after Catalog is healthy, so the next stack start does not depend on a manual curl.
- When Kafka is configured, Catalog applies Marten schema on startup so `mt_doc_product` exists before the connector registers.

### Files

| Change | File |
|--------|------|
| Missing index → empty page | `src/BuildingBlocks/BuildingBlock/Search/ElasticsearchSearchStore.cs` (`IsMissingIndex`) |
| Schema apply when Kafka is set | `src/Services/eShop.Catalog.Api/DependencyInjection.cs` (`UsePipline`) |
| Auto-register Debezium connector | `deploy/compose.yaml`, `deploy/compose.override.yaml` (`connect-register`) |
| Connector config | `deploy/debezium/catalog-product-connector.json` |
| Tests | `tests/BuildingBlocks/BuildingBlock.Tests/SearchStoreTests.cs` |

---

## 2. Vietnamese full-name search returned zero hits

### Symptom

Query:

```http
GET /products?name=Đầm Alice - size 1 (Xanh dương)
```

Response before fix:

```json
{ "pageNumber": 1, "pageSize": 10, "totalCount": 0, "items": [] }
```

The product existed in `catalog-products`. Single ASCII tokens such as `Lily` or `Alice` still matched.

### Why (before)

Name search used a wildcard on the **analyzed** `name` text field:

```csharp
new WildcardQuery
{
    Field = "name",
    Value = $"*{value.ToLowerInvariant()}*"
}
```

Elasticsearch `standard` analyzer splits the stored name into tokens:

```text
Đầm Alice - size 1 (Xanh dương)
→ đầm, alice, size, 1, xanh, dương
```

A wildcard on a `text` field runs against **one token at a time**. The full name (spaces, hyphen, parentheses) is not a token, so the query matched nothing.

Spec rule is case-insensitive **contains on the whole name** (same as `InMemorySearchStore` / `string.Contains`). Token wildcards do not implement that.

Probe (old behavior):

| Term | Result |
|------|--------|
| `Lily` / `Alice` | matched |
| `Đầm` / `đầm` | matched (one token) |
| `dam` / `trang` (no diacritics) | no match |
| `ầm Li` (crosses token boundary) | no match |
| full name `Đầm Alice - size 1 (Xanh dương)` | no match |

### After

Name contains uses the full-string `keyword` subfield, case-insensitive:

```csharp
new WildcardQuery
{
    Field = "name.keyword",
    Value = $"*{value.Trim()}*",
    CaseInsensitive = true
}
```

The same full Vietnamese name now matches. No reindex was required: auto-mapping already created `name.keyword`.

This is still **not** accent-folding. `dam` still does not match `Đầm`. That is unchanged; spec only requires case-insensitive contains.

### Files

| Change | File |
|--------|------|
| Keyword contains query | `src/BuildingBlocks/BuildingBlock/Search/ElasticsearchSearchStore.cs` (`ContainsQuery`) |
| Query-shape test | `tests/BuildingBlocks/BuildingBlock.Tests/SearchStoreTests.cs` |
| Handler behavior test | `tests/Services/eShop.Catalog.Api.Tests/GetProductsHandlerTests.cs` |

---

## How to re-check

```bash
# Search sync + connector
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait kafka connect elasticsearch kibana kafka-ui connect-register

# Connector should exist
curl http://localhost:8083/connectors/eshop-catalog-product/status

# Vietnamese full name
curl -G "http://localhost:5000/products" --data-urlencode "name=Đầm Alice - size 1 (Xanh dương)"
```

Empty catalog with Elasticsearch up and no index yet: `GET /products` should be **200** with empty `items`, not 503.
