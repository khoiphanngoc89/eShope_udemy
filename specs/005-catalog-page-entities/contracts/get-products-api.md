# Contract: Catalog paginated browse

Public browse path used by service-level checks. Base URL is the `WebApplicationFactory` host. No Grafana. No live Postgres. No live Elasticsearch — the host uses an in-memory search store unless the test is proving outage.

Auth: none.

The path already exists (`GET /products`). This pass changes the **read source** to the search index and adds a service error when that index is unavailable. It does not rename query parameters.

## 1. Get a page of products (FR-001, FR-002, FR-003, FR-005, FR-018, FR-019)

```http
GET /products?pageNumber=1&pageSize=10
GET /products?pageNumber=1&pageSize=10&name=bike&costPrice=1299.50&category=outdoor
```

Query names include `pageNumber`, `pageSize`, `orderBy`, `orderDirection`, `name`, `costPrice`, `sewingCost`, `fabricCostAccessoriesCost`, `packagingCost`, and `category`. Defaults: `pageNumber=1`, `pageSize=10`. Search and sort fields are optional.

**Read source**: search index (`ISearchStore<Product>` in BuildingBlock). This path MUST NOT query the catalog store.

**Success**

- Status: `200 OK`
- Body is the shared page model (`PaginatedResult<Product>`):

| Field | Type |
|-------|------|
| `pageNumber` | integer |
| `pageSize` | integer |
| `totalCount` | integer |
| `items` | array of product |
| `orderBy` | JSON string enum (`CreatedAt`, `LastModified`, `Name`, `CostPrice`, `SewingCost`, `FabricCostAccessoriesCost`, `PackagingCost`, `Amount`) |
| `orderDirection` | JSON string enum (`Asc`, `Desc`) |

Each product includes existing catalog fields (`id`, `name`, `amount`, cost fields, `description`, `imageFile`, `categories`) plus shared traits (`createdAt`, `lastModified`, `createdBy`, `lastModifiedBy`, `isDeleted`, `isActive`).

**Rules**

- `items.length` ≤ `pageSize`
- `totalCount` is the number of products in the filtered browse set, not `items.length`
- `items` MUST NOT include a product with `isDeleted: true` or `isActive: false`
- Empty catalog or no matches: `items` is `[]` and `totalCount` is `0`
- Page past the last match: `items` is `[]` and `totalCount` is unchanged
- `name`: case-insensitive contains; omitted or blank means no name filter
- Cost fields: exact decimal match; omitted means no filter on that field
- `category`: case-insensitive exact match against one listed category
- Combined search fields are AND
- Two identical requests against an unchanged index return the same `items` in the same order

**Failure of the feature (tests MUST fail)**

- Non-200 for a valid page request when the index is up
- A catalog-store-only product (not in the search store) appears in `items`
- A deleted or inactive product appears in `items`
- `totalCount` does not match the filtered browse set
- Order differs across two identical requests

## 2. Invalid page or search request (FR-004, FR-020)

```http
GET /products?pageNumber=0
GET /products?pageSize=0
GET /products?pageSize=101
GET /products?costPrice=-1
```

**Success of the check**

- Status: `400 Bad Request`
- Body is a clear validation failure (not an empty 200 page)

## 3. Search index unavailable (FR-023, FR-024)

```http
GET /products?pageNumber=1&pageSize=10
```

When the search store cannot serve the query.

**Success of the check**

- Status: `503 Service Unavailable`
- Body is `{ message }` (no stack)
- Items are not returned from the catalog store
- The failed-request log still includes request name, duration, and stack (FR-021)
- `GET /products/{id}` for that same product also returns 503

**Failure of the feature**

- HTTP 200 with `items: []` while the index is down
- HTTP 200 with products loaded from the catalog store
- Create failing solely because the index is down
- Get-by-id returning 200 from the catalog store while the index is down

## Out of scope

- `DELETE /products/{id}` or activate/deactivate routes
- Changing `POST /products` or `PUT /products/{id}` paths
- Waiting in create/update until the product is searchable
