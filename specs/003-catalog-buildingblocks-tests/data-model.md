# Data Model: Catalog and BuildingBlocks Automated Tests

This feature does not add production entities. It checks the Catalog product already stored by create-product, plus the check-suite shape operators run.

## Product

Stored by Catalog create-product. Isolated and service-level checks assert these fields after a valid create.

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| Id | UUID | yes | New non-empty identity on each successful create. Two creates in one run MUST differ (FR-011). |
| Name | string | yes | Echoed from the create-product action. Current Catalog does not reject empty; checks record that. |
| Price | decimal | yes | Echoed from the action. Current Catalog does not reject negatives; checks record that. |
| Description | string | yes | Echoed from the action. |
| ImageFile | string | yes | Echoed from the action. |
| Categories | list of string | yes | Echoed from the action (may be empty list). |

### Persistence rules

- Isolated check: after the handler runs, the substitute session contains a `Product` with the same Id as the result and matching fields (FR-001).
- Service-level check: after `POST /products`, the substitute session can load that Id and fields match (FR-002).
- Missing `ConnectionStrings:CatalogDb`: no product is stored; startup fails before handling requests (FR-007).

## Create-product action

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| Name, Price, Description, ImageFile, Categories | same as Product | yes | Public JSON body on `POST /products`. |
| Result.Id | UUID | yes | Returned to the caller; Location URI includes that Id. |

No read/update/delete actions in this model (FR-012).

## Isolated check vs service-level check

| Kind | Entry | Store | Log viewer |
|------|--------|--------|------------|
| Isolated Catalog | `CreateProductHandler.Handle` | In-suite session substitute | Must not be required |
| Service-level Catalog | `POST /products` | Same substitute behind the host | Must not be required |
| Isolated BuildingBlock | sample `ICommand` send; `AddAppCarter` host | None | Must not be required |

## Shared building block (test view)

| Concept | What checks prove |
|---------|-------------------|
| Command type | A test command implementing `ICommand<T>` is handled by `ICommandHandler<T,T>` through `ISender` |
| Request-entry registration | `AddAppCarter` registers a test (or Catalog) Carter module so a mapped route responds |

Logging building-block configuration is **not** in this model (owned by feature 001).
