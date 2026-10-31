# Contract: BuildingBlock item base and page model

Consumers are ASP.NET Core services (Catalog first). Isolated checks live in `tests/BuildingBlocks/BuildingBlock.Tests`. Do not use `BuildBlock.Logging.Tests` for these cases (FR-016).

## 1. Shared item traits (FR-008, FR-014)

```text
IAuditable
IDeletable
IActivable
BaseEntity : IAuditable, IDeletable, IActivable
```

**Success**

- `BaseEntity` exposes `Id`, `CreatedAt` and `LastModified` (`DateTimeOffset`, UTC), `CreatedBy`, `LastModifiedBy`, `IsDeleted`, `IsActive`
- A test type can inherit `BaseEntity` without Catalog
- A new instance is not deleted and is active (`IsDeleted = false`, `IsActive = true`)

**Failure of the feature**

- Catalog `Product` redefines those fields instead of inheriting `BaseEntity`
- The types live in Catalog instead of `BuildingBlock`

## 2. Shared page model (FR-012, FR-014)

```text
PaginationRequest(PageNumber = 1, PageSize = 10)
PaginatedResult<T>(PageNumber, PageSize, TotalCount, Items)
```

**Success**

- A test can construct `PaginationRequest` and get defaults `1` and `10`
- A test can construct `PaginatedResult<T>` with a page of items and a total count without starting Catalog

**Failure of the feature**

- Catalog invents a one-off page DTO and does not use `PaginatedResult<T>`
- Isolated checks require the Catalog host

## Out of scope

- Serilog / `AddAppLogging`
- Public delete or activate helpers
- Changing `ICommand` / `AddAppCarter` contracts from feature 003
