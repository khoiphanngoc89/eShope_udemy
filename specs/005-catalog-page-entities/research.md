# Research: Paginated Catalog Browse and Shared Item Traits

## 1. Shared item base lives in BuildingBlock, not Catalog

**Decision**: Add public `IAuditable`, `IDeletable`, `IActivable`, and abstract `BaseEntity` under `src/BuildingBlocks/BuildingBlock/Models/`. Catalog `Product` inherits `BaseEntity` and drops its own `Id`. No second copy of identity or lifecycle fields in Catalog.

**Rationale**: FR-008, FR-009, and the request name those contracts and require the core model in `BuildingBlock`. Later services can reuse the same base (US2 scenario 6). Ponytail rung 1: reuse the existing building-block project; do not add a new library.

**Alternatives considered**:
- Keep `Id` only on `Product` and add the three interfaces in Catalog — rejected; FR-008/FR-009 require the shared base.
- New `BuildingBlock.Entities` project — extra project, not requested.
- Marten `IDocument` / soft-delete package — extra dependency for a few fields.

## 2. Audit, deletion, and activable field names

**Decision**:

| Trait | Fields | Defaults |
|-------|--------|----------|
| Identity on `BaseEntity` | `Id` (`Guid`) | Empty until create sets it |
| `IAuditable` | `CreatedAt`, `LastModified` (`DateTimeOffset`, UTC); `CreatedBy`, `LastModifiedBy` (`string?`) | Times set on create; actor fields stay null |
| `IDeletable` | `IsDeleted` (`bool`) | `false` |
| `IActivable` | `IsActive` (`bool`) | `true` |

`GenericRepository.Create` sets `CreatedAt` and `LastModified` to the same `DateTimeOffset.UtcNow` value. `GenericRepository.Update` refreshes `LastModified` while preserving `CreatedAt`; delete uses the same update path. Create-product sets identity and lifecycle defaults. Actor fields remain null until Catalog has an authenticated caller.

**Rationale**: Spec assumptions: times only until Catalog has callers; new products are active and not deleted; actor fields may exist and stay empty. UTC avoids local-clock drift in checks (SC-003, SC-004).

**Alternatives considered**:
- `DateTime` — UTC conveys the intended timezone, but `DateTimeOffset` makes the offset explicit and is the shared audit timestamp type.
- Omit actor fields — allowed, but the spec permits them and `IAuditable` is otherwise only timestamps.
- Rename `IActivable` to `IActivatable` — rejected; requested name is `IActivable`.

## 3. Shared page model in BuildingBlock

**Decision**: Add `PaginationRequest` (page number default `1`, page size default `10`) and `PaginatedResult<T>` (page number, page size, total count, items) under `src/BuildingBlocks/BuildingBlock/Models/`. Catalog browse uses these types instead of a Catalog-only page DTO.

**Rationale**: FR-012. Isolated BuildingBlock checks can construct both types without Catalog (FR-014). Defaults match the spec assumption (page 1, size 10, max size 100 enforced by Catalog validation, not by the record itself).

**Alternatives considered**:
- Catalog-only page DTO — rejected by FR-012.
- Cursor pagination — not requested; page number is specified.
- Put max page size on `PaginationRequest` — Catalog validation already owns the max (FR-004); keep the shared record dumb.

## 4. Browse query uses standard LINQ on `IDocumentSession.Query<Product>()`

**Decision**: `GetProductsHandler` reads `ICatalogUnitOfWork.Products.FindAll()` (already applies `IsActive` and not `IsDeleted`), then optional name / price / category `Where`, then orders by `LastModified` descending then `Id` ascending, then `ToMaterializedListAsync`. If the provider is Marten, that helper uses Marten `ToListAsync` (Marten 9 forbids sync `ToList()`). Isolated tests still use `InMemoryDocumentSession` whose provider is `System.Linq`; the helper then uses standard `ToList()`.

**Rationale**: FR-015 forbids a live operator store. Feature 003 already uses an in-suite `IDocumentSession` substitute. Marten async extensions do not run on `EnumerableQuery`. Standard LINQ runs on both Marten’s queryable and the substitute.

**Alternatives considered**:
- Testcontainers PostgreSQL — rejected by FR-015 and SC-007.
- Sync `ToList()` in production — rejected; Marten 9 throws “only asynchronous data access is supported.”
- Page only in service-level tests — fails isolated browse checks (FR-013).

## 5. Public browse path and validation

**Decision**: `GET /products?pageNumber=1&pageSize=10&name=&price=&category=`. Page fields bind from `PaginationRequest` defaults. Optional `name` is case-insensitive contains; optional `price` is exact; optional `category` is case-insensitive exact match against one listed category. Combined filters AND. Blank name or category is ignored. Handler query is `IQuery<PaginatedResult<Product>>` with FluentValidation: page number ≥ 1, page size ≥ 1 and ≤ 100, price ≥ 0 when supplied. Missing page values use the request defaults. Validation failures throw the existing `ValidationException`; extend `WithException` to map that to HTTP 400. Empty, no-match, or past-the-end pages return HTTP 200 with empty `items` and the filtered total.

**Rationale**: Same collection path as create (`POST /products`). Carter already maps `/products/{id}` for get-by-id and update. FR-001–FR-004. Reuse `ValidationBehavior` and `WithException` instead of a new filter.

**Alternatives considered**:
- `GET /products/page` — extra path; not needed.
- Clamp invalid page size — spec requires reject, not clamp (FR-004).
- New global exception middleware — extra layer; `WithException` already exists.

## 6. Get-by-id treats deleted as not found

**Decision**: After `LoadAsync`, if the product is null or `IsDeleted`, throw the existing `ProductNotFoundException`. Add `.WithException()` on `GetByIdEndpoint` so the public path returns HTTP 404 with the existing `{ message }` body. Do not add a delete endpoint (FR-017). Isolated checks mark `IsDeleted` on the in-suite store.

**Rationale**: FR-006, FR-007, SC-005. Get-by-id already throws `ProductNotFoundException` for missing ids; Update already uses `WithException`. Get-by-id does not, so a service-level not-found check would see 500 today.

**Alternatives considered**:
- Return the deleted product with `isDeleted: true` — rejected by FR-007.
- New delete API to set the flag — out of scope (FR-017).
- Change `GetById` to `IQuery` in this feature — behavior-preserving rename; skip (Ponytail).

## 7. Create and update keep public paths; set traits in handlers

**Decision**: Do not change `POST /products` or `PUT /products/{id}` routes or request bodies. `CreateProductHandler` assigns identity (already) plus audit/active/deleted defaults. `UpdateProductHandler` keeps `Id` and `CreatedAt`, then sets `LastModified`. Existing create-product tests stay; add trait assertions rather than rewriting those tests (FR-016). Add isolated update-product checks for SC-004. No new update service-level suite required (FR-013 lists get-by-id and browse only).

**Rationale**: FR-010, FR-011, FR-016. Mapster `Adapt` on update maps command fields only (no audit properties), so `CreatedAt` is left intact if the handler then refreshes `LastModified`.

**Alternatives considered**:
- Marten document session listener for audit — extra hook, harder to test in isolation.
- Rewrite create-product tests from scratch — rejected by FR-016.

## 8. Automated checks layout

**Decision**: Reuse `tests/Services/eShop.Catalog.Api.Tests` and `tests/BuildingBlocks/BuildingBlock.Tests`. Add isolated BuildingBlock checks for `BaseEntity` and `PaginatedResult<T>`. Add Catalog isolated + service-level checks for get-by-id and get-products. Extend `InMemoryDocumentSession` for `Query<Product>()`. Do not edit `BuildBlock.Logging.Tests`. Do not require Grafana/Loki.

**Rationale**: FR-013, FR-014, FR-015, FR-016, SC-006–SC-008. Same factory and in-suite store as feature 003.

**Alternatives considered**:
- New test project for pagination — extra project for a few files.
- Live compose catalog store for service-level checks — rejected by FR-015.

## 9. What not to touch

**Decision**: Do not add public delete or activate routes. Name, price, and category search stay as optional query parameters on `GET /products`. Do not change Serilog, OTLP, or `deploy/` compose. Do not rewrite logging tests or replace create-product tests. Do not move `Product` out of Catalog (only its base moves to BuildingBlock).

**Rationale**: FR-016, FR-017, spec edge cases, hosting feature 004 stays independent.
