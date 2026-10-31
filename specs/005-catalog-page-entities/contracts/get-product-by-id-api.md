# Contract: Catalog get-by-id

Public get-by-id path used by service-level checks. Base URL is the `WebApplicationFactory` host. No Grafana. No live Postgres.

Auth: none.

The path already exists (`GET /products/{id}`). This feature adds not-found-for-deleted and automated checks. It does not rename the path.

## 1. Get a stored, not-deleted product (FR-006)

```http
GET /products/{id}
```

`id` is a UUID.

**Success**

- Status: `200 OK`
- Body includes the product with catalog fields and shared traits:

| Field | Type |
|-------|------|
| `id` | UUID (matches the path) |
| `name`, `price`, `description`, `imageFile`, `categories` | same as stored |
| `createdAt`, `lastModified` | date-time |
| `createdBy`, `lastModifiedBy` | string or null |
| `isDeleted` | `false` |
| `isActive` | `true` |

An inactive or deleted product MUST return `404` (FR-007). The get query uses the shared base query (`IsActive` and not `IsDeleted`).

**Failure of the feature (tests MUST fail)**

- Non-200 for a stored, not-deleted product
- Body `id` does not match the path
- Shared traits missing

## 2. Missing or deleted product (FR-007)

```http
GET /products/{id}
```

When no product exists for `id`, or the stored product has `isDeleted: true`.

**Success of the check**

- Status: `404 Not Found`
- Body includes `message` (existing `ProductNotFoundException` text)

Deleted products MUST NOT be returned as `200`.

## Out of scope

- Changing the get-by-id path
- Public delete that sets `isDeleted` (checks mark the isolated store directly)
- Adding query filters to get-by-id
