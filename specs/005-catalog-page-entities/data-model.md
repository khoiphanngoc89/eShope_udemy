# Data Model: Paginated Catalog Browse and Shared Item Traits

## BaseEntity (shared item base)

Owned by `BuildingBlock`. Catalog `Product` uses it. Requested name: `BaseEntity`.

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| Id | UUID | yes | Stable identity. Create assigns a new non-empty value. Update MUST NOT change it. |

Implements the three traits below. Later services inherit this type instead of copying Catalog `Product`.

## IAuditable

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| CreatedAt | date-time (UTC) | yes | Set on create. Update MUST NOT change it. |
| LastModified | date-time (UTC) | yes | Set on create (same instant as `CreatedAt`). Update MUST set a later value. |
| CreatedBy | string or null | no | Empty until Catalog has an authenticated caller. |
| LastModifiedBy | string or null | no | Empty until Catalog has an authenticated caller. |

## IDeletable

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| IsDeleted | boolean | yes | Default `false`. Default browse omits `true`. Get-by-id of `true` is not found. No public delete action in this feature; checks may set this on the isolated store. |

## IActivable

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| IsActive | boolean | yes | Default `true` on create. Inactive products fail the base query: omitted from browse and get-by-id is not found. No public activate action in this feature. |

## Product

Catalog item. Inherits `BaseEntity` (FR-009). Does **not** redefine `Id` or the trait fields.

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| Name | string | yes | Unchanged from create/update. |
| Price | decimal | yes | Unchanged from create/update. |
| Description | string | yes | Unchanged from create/update. |
| ImageFile | string | yes | Unchanged from create/update. |
| Categories | list of string | yes | Unchanged from create/update. |

### State transitions

```text
Create → Active, not deleted, CreatedAt = LastModified = now
Update → same Id, same CreatedAt, LastModified = now; IsActive / IsDeleted unchanged by update body
Mark deleted (store only) → hidden from default browse; get-by-id = not found
Mark inactive (store only) → hidden from default browse; get-by-id = not found
```

### Persistence rules

- Isolated create: stored `Product` matches result id, product fields, and trait defaults (FR-010).
- Isolated update: stored `Product` keeps id and `CreatedAt`; `LastModified` is later (FR-011).
- Isolated / service-level get-by-id: load by id; null or `IsDeleted` → not found (FR-006, FR-007).
- Isolated / service-level browse: query not-deleted products only (FR-005).

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

- Include: products with `IsActive = true` and `IsDeleted = false`.
- Exclude: products with `IsDeleted = true`.
- Order: `LastModified` descending, then `Id` ascending (FR-018, SC-009).
- Slice: `Skip((PageNumber - 1) * PageSize)`, `Take(PageSize)`.

## Product search (GET /products)

Optional query fields. Not part of `PaginationRequest`. Catalog browse applies them after the default browse set.

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| Name | string or omitted | no | Case-insensitive contains on `Product.Name`. Blank or omitted = no name filter. |
| Price | decimal or omitted | no | Exact match on `Product.Price`. Omitted = no price filter. Catalog rejects `< 0`. |
| Category | string or omitted | no | Case-insensitive exact match against one entry in `Product.Categories`. Blank or omitted = no category filter. |

Combined fields AND. `TotalCount` and paging use the filtered set.

### Filtered browse set

- Start from the default browse set.
- Apply each supplied search field.
- Inactive or deleted products stay excluded even when they would match name, price, or category.

## Isolated check vs service-level check

| Kind | Entry | Store | Log viewer |
|------|--------|--------|------------|
| Isolated BuildingBlock | construct `BaseEntity` / `PaginatedResult<T>` | None | Must not be required |
| Isolated Catalog get-by-id | `GetByIdHandler.Handle` | In-suite session substitute | Must not be required |
| Isolated Catalog browse | `GetProductsHandler.Handle` | Same substitute, including name/price/category search | Must not be required |
| Isolated Catalog update | `UpdateProductHandler.Handle` | Same substitute | Must not be required |
| Service-level get-by-id | `GET /products/{id}` | Same substitute behind the host | Must not be required |
| Service-level browse | `GET /products` | Same substitute behind the host | Must not be required |
| Existing create-product | unchanged tests | Same substitute | Must not be required |
