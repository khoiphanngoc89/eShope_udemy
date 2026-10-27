# Contract: Catalog create-product

Public create path used by service-level checks. Base URL is the `WebApplicationFactory` host. No Grafana. No live Postgres.

Auth: none (Catalog has no identity on this path today).

## 1. Create product (FR-002, FR-010, FR-011)

```http
POST /products
Content-Type: application/json
```

**Body**

| Field | Type | Required |
|-------|------|----------|
| `name` | string | yes |
| `price` | number | yes |
| `description` | string | yes |
| `imageFile` | string | yes |
| `categories` | array of string | yes |

**Success**

- Status: `201 Created`
- `Location` includes `/products/{id}`
- Body includes `id` (UUID, non-empty)
- The in-suite store can load a `Product` with that `id` and matching fields

**Two successive calls**

- Both succeed
- The two `id` values MUST differ (FR-011)

**Failure of the feature (tests MUST fail)**

- Non-201
- Missing or empty `id`
- Store has no product for that `id`, or fields do not match

## 2. Missing catalog store setting (FR-007)

Host started with `ConnectionStrings:CatalogDb` missing or whitespace.

**Success of the check**

- Host start / client creation fails
- Error message contains `ConnectionStrings:CatalogDb`

Not an HTTP status from `/products` — startup never serves the route.

## Out of scope

- `GET` / `PUT` / `DELETE` `/products`
- Creating datasources or calling Grafana
- Changing Catalog `Serilog` settings
