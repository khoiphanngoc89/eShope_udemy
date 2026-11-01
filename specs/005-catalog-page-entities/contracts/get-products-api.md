# Contract: Catalog paginated browse

Public browse path used by service-level checks. Base URL is the `WebApplicationFactory` host. No Grafana. No live Postgres.

Auth: none.

## 1. Get a page of products (FR-001, FR-002, FR-003, FR-005, FR-018, FR-019)

```http
GET /products?pageNumber=1&pageSize=10
GET /products?pageNumber=1&pageSize=10&name=bike&price=1299.50&category=outdoor
```

Query names include `pageNumber`, `pageSize`, `orderBy`, `orderDirection`, `name`, the product cost fields, and `category`. Defaults: `pageNumber=1`, `pageSize=10`, `orderBy=CreatedAt`, `orderDirection=desc`. Search and sort fields are optional.

**Success**

- Status: `200 OK`
- Body is the shared page model (`PaginatedResult<Product>`):

| Field | Type |
|-------|------|
| `pageNumber` | integer |
| `pageSize` | integer |
| `totalCount` | integer |
| `items` | array of product |

Each product includes existing catalog fields (`id`, `name`, `price`, `description`, `imageFile`, `categories`) plus shared traits (`createdAt`, `lastModified`, `createdBy`, `lastModifiedBy`, `isDeleted`, `isActive`).

**Rules**

- `items.length` ≤ `pageSize`
- `totalCount` is the number of products in the filtered browse set, not `items.length`
- `items` MUST NOT include a product with `isDeleted: true`
- `items` MUST NOT include a product with `isActive: false`
- Empty catalog or no matches: `items` is `[]` and `totalCount` is `0`
- Page past the last match: `items` is `[]` and `totalCount` is unchanged
- `name`: case-insensitive contains on product name; omitted or blank means no name filter
- `price`: exact decimal match; omitted means no price filter
- `category`: case-insensitive exact match against one listed category; omitted or blank means no category filter
- Combined search fields are AND
- Default order: `createdAt` newest first, then `id` ascending
- `orderBy`: one of `CreatedAt`, `LastModified`, `Name`, `CostPrice`, `SewingCost`, `FabricCostAccessoriesCost`, `PackagingCost`, or `Amount`
- `orderDirection`: `asc` or `desc`
- Two identical requests against an unchanged store return the same `items` in the same order

**Failure of the feature (tests MUST fail)**

- Non-200 for a valid page request
- Missing page fields
- A deleted product appears in `items`
- `totalCount` does not match the filtered browse set
- A product that fails the name, price, or category filter appears in `items`
- Order differs across two identical requests

## 2. Invalid page or search request (FR-004, FR-020)

```http
GET /products?pageNumber=0
GET /products?pageSize=0
GET /products?pageSize=101
GET /products?price=-1
```

**Success of the check**

- Status: `400 Bad Request`
- Body is a clear validation failure (not an empty 200 page)

- `DELETE /products/{id}` or activate/deactivate routes
- Changing `POST /products` or `PUT /products/{id}` paths
