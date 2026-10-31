# Feature Specification: Paginated Catalog Browse and Shared Item Traits

**Feature Branch**: `005-catalog-page-entities`

**Created**: 2026-10-31

**Status**: Draft

**Input**: User description: "implement get product pagination and add missing unit tests for all Catalog.Api search; put the core model in BuildingBlock; declare IAuditable, IDeletable, and IActivable; implement BaseEntity; Product inherits from BaseEntity; clients can search the product page by name, price, or category"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browse a page of catalog products (Priority: P1)

A catalog client needs to see products without loading the whole catalog at once. They ask for one page of products using a page number and a page size. They may also narrow that page by name, price, or category. Catalog returns only that page’s matching products plus enough page information to know how many matches exist and how to ask for another page. An empty catalog, no matches, or a page past the last match returns an empty page rather than a failure.

**Why this priority**: Catalog can create, fetch one product, and update a product, but it cannot yet return a bounded, filterable list. Until a client can page and search products, “search Catalog” is only get-by-id.

**Independent Test**: Create several products in an isolated catalog store. Request page 1 with a page size smaller than the total. Confirm the response contains only that many products and reports the total count. Request a later empty page and confirm it is empty, not an error.

**Acceptance Scenarios**:

1. **Given** more products exist than one page can hold, **When** a client asks for the first page with a valid page size, **Then** Catalog returns only that page’s products and the total number of products that belong in the default browse set.
2. **Given** a catalog with products, **When** a client asks for a page number beyond the last product, **Then** Catalog returns an empty product list together with the same total count, not a not-found or failure result.
3. **Given** an empty catalog, **When** a client asks for the first page, **Then** Catalog returns an empty product list and a total of zero.
4. **Given** a client sends a page number less than 1 or a page size less than 1 or above the documented maximum, **When** they ask for a page, **Then** Catalog rejects the request with a clear validation failure.
5. **Given** stored products with different names, **When** a client asks for a page with a name search, **Then** Catalog returns only active, not-deleted products whose name contains that text (case-insensitive) and a total that matches that filtered set.
6. **Given** stored products with different prices, **When** a client asks for a page with a price search, **Then** Catalog returns only active, not-deleted products with that exact price and a total that matches that filtered set.
7. **Given** stored products in different categories, **When** a client asks for a page with a category search, **Then** Catalog returns only active, not-deleted products that list that category (case-insensitive) and a total that matches that filtered set.
8. **Given** more than one search field is sent, **When** a client asks for a page, **Then** Catalog applies every supplied field together (a product must match all of them).
9. **Given** a client sends a negative price search, **When** they ask for a page, **Then** Catalog rejects the request with a clear validation failure.

---

### User Story 2 - Give every catalog product shared item traits (Priority: P2)

A developer adding catalog items (and later other items that reuse the same shared building block) needs every product to carry a stable identity plus three common traits: audit (when it was created and last changed), deletion (whether it is marked deleted), and activable (whether it is active). New products start active and not deleted, with created and last-changed times set. An update keeps the identity and created time and refreshes the last-changed time. The default product page and get-by-id both apply the shared base query: only active, not-deleted products are visible. Fetching a deleted or inactive product by identity behaves as not found.

**Why this priority**: Pagination is only useful if every listed product has a consistent identity and lifecycle. These traits belong in the shared building block so later services do not invent their own item base. They do not require new public delete or activate actions in this feature.

**Independent Test**: Create a product and confirm it has identity, created time, last-changed time, is not deleted, and is active. Update it and confirm identity and created time stay the same while last-changed time moves forward. Mark a product deleted in the store and confirm the default page omits it and get-by-id treats it as not found.

**Acceptance Scenarios**:

1. **Given** a valid create-product action, **When** the product is stored, **Then** it has a new identity, a created time, a last-changed time, is not deleted, and is active.
2. **Given** an existing product, **When** a valid update-product action succeeds, **Then** the identity and created time are unchanged and the last-changed time is later than before the update.
3. **Given** a product that is marked deleted, **When** a client asks for the default product page, **Then** that product is not included.
4. **Given** a product that is marked deleted, **When** a client fetches it by identity, **Then** Catalog reports that the product was not found.
5. **Given** a product that is inactive or deleted, **When** a client fetches it by identity, **Then** Catalog reports that the product was not found.
6. **Given** the shared building block’s item base, **When** a later service needs the same identity, audit, deletion, and activable traits, **Then** it can reuse that base without copying Catalog’s product shape.

---

### User Story 3 - Prove Catalog search paths with automated checks (Priority: P3)

A developer or reviewer needs confidence that Catalog’s search paths work: get a product by identity, get a page of products, and filter that page by name, price, or category. They run isolated automated checks without the log viewer stack, and service-level automated checks against an isolated catalog store. Those checks fail if get-by-id stops returning the stored product, if a missing or deleted identity is not reported as not found, or if a page stops returning the correct slice, total, or filtered set. Existing create-product checks still pass. Isolated checks also prove the shared item base and the shared page model can be used without standing up Catalog.

**Why this priority**: Get-by-id already exists and has no automated checks. The new page path will have none until this story lands. Coverage is required for daily confidence, but it is not the first client-facing browse capability.

**Independent Test**: With the log viewer stack stopped, run isolated Catalog and shared-building-block checks, then run service-level get-by-id and page checks against an isolated store. Existing create-product checks still pass.

**Acceptance Scenarios**:

1. **Given** a stored product that is not deleted, **When** isolated and service-level get-by-id checks run, **Then** they receive that product’s name, price, description, image file, categories, identity, audit times, deletion state, and active state.
2. **Given** no product exists for an identity, or the product is marked deleted, **When** those get-by-id checks run, **Then** they prove Catalog reports not found.
3. **Given** several stored products, **When** isolated and service-level page checks run, **Then** they prove one page returns only that page’s products and the correct total for the default browse set, including when a name, price, or category search is supplied.
4. **Given** the existing create-product isolated and service-level checks, **When** this feature is complete, **Then** 100% of those previously passing checks still pass.
5. **Given** the log viewer stack is stopped, **When** a developer runs the isolated checks for this feature, **Then** they complete without starting Grafana or Loki.

---

### Edge Cases

- Empty catalog: the first page is empty and the total is zero; this is success, not an error.
- Page past the last product: empty product list, total unchanged, not a not-found result.
- Page number less than 1, page size less than 1, or page size above the documented maximum: validation failure.
- Default page omits deleted products; those products still exist in the store.
- Default page includes only products that are active and not deleted.
- Get-by-id of a missing identity: not found (existing Catalog rule).
- Get-by-id of a deleted or inactive product: not found.
- Create-product still returns a new identity and stores name, price, description, image file, and categories; new shared traits are added without changing that public create path.
- Update-product still updates name, price, description, image file, and categories on the existing identity; last-changed time is refreshed; created time is not rewritten.
- Two page requests with the same page number and page size against an unchanged catalog return the same products in the same order.
- Isolated and service-level checks use an isolated catalog store, not the operator’s everyday local catalog store, and do not require the log viewer stack.
- Public delete-product and activate/deactivate actions are not required to complete this feature; the deletion and activable traits must exist on the product so those actions can be added later.
- Name, price, and category search are optional on the product page. Omitted or blank name and category mean “no filter on that field.” A supplied name is a case-insensitive contains match. A supplied price is an exact match. A supplied category is a case-insensitive exact match against one of the product’s categories. Combined filters must all match.
- A negative price search is a validation failure.
- No matches for a valid search is an empty page and a total of zero, not a not-found result.
- This feature must not replace or rewrite the existing create-product or shared logging test suites.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Clients MUST be able to request one page of catalog products by page number and page size on a public Catalog browse path.
- **FR-002**: A successful page MUST include the products for that page and the total count of products in the filtered browse set (the default browse set after optional name, price, and category search).
- **FR-003**: An empty catalog or a page past the last product MUST return an empty product list and the correct total, not a failure.
- **FR-004**: Catalog MUST reject a page number less than 1, a page size less than 1, or a page size above the documented maximum, with a clear validation failure.
- **FR-005**: The default browse set MUST include only products that are active and not marked deleted. Every get query MUST apply this base-query rule.
- **FR-006**: Get-by-id of a stored, not-deleted product MUST return that product, including shared item traits.
- **FR-007**: Get-by-id of a missing, deleted, or inactive product MUST report not found.
- **FR-008**: The shared building block MUST provide a reusable item base that every catalog product uses. That base MUST include a stable identity plus audit, deletion, and activable traits. Requested names for those contracts are `BaseEntity`, `IAuditable`, `IDeletable`, and `IActivable`.
- **FR-009**: Catalog Product MUST use that shared item base so it carries identity, audit, deletion, and activable traits without defining its own copy of those fields.
- **FR-010**: A successful create-product MUST store a product that is active, not deleted, and has created and last-changed times set, without changing Catalog’s public create-product path.
- **FR-011**: A successful update-product MUST keep identity and created time unchanged and MUST refresh last-changed time, without changing Catalog’s public update-product path.
- **FR-012**: The shared building block MUST provide a reusable page model (page number, page size, total count, and the page’s items) so Catalog does not invent a one-off page shape.
- **FR-013**: Catalog MUST have isolated and service-level automated checks for get-by-id (found and not-found) and for paginated browse.
- **FR-014**: Isolated automated checks MUST prove the shared item base and the shared page model can be used without starting Catalog or the log viewer stack.
- **FR-015**: Isolated and service-level checks for this feature MUST use an isolated catalog store and MUST complete without the log viewer stack.
- **FR-016**: This feature MUST NOT replace, remove, or rewrite the existing create-product or shared logging test suites.
- **FR-017**: This feature MUST NOT require new public delete-product or activate/deactivate actions as a completion condition.
- **FR-018**: Products on a page MUST appear in a documented stable order so the same page request against an unchanged catalog is repeatable.
- **FR-019**: Clients MUST be able to optionally search the product page by name, price, or category. A name match is case-insensitive contains. A price match is exact. A category match is a case-insensitive exact match against one listed category. When more than one search field is supplied, a product MUST match all of them.
- **FR-020**: Catalog MUST reject a negative price search with a clear validation failure. Omitted or blank name and category MUST mean no filter on that field.

### Key Entities

- **Product**: A catalog item with identity, name, price, description, image file, categories, plus the shared item traits below.
- **Shared item base**: The reusable identity-and-lifecycle shape provided by the shared building block. Catalog Product uses it. Requested name: `BaseEntity`.
- **Audit trait**: Created time and last-changed time on an item. Requested name: `IAuditable`. Actor identity is not required until Catalog has authenticated callers.
- **Deletion trait**: Whether an item is marked deleted. Requested name: `IDeletable`. Marking deleted hides the item from the default page and from get-by-id.
- **Activable trait**: Whether an item is active. Requested name: `IActivable`. New products start active. Inactive products are hidden from every get query.
- **Product page**: One bounded slice of the filtered browse set, with page number, page size, total count, and that slice’s products.
- **Default browse set**: Products that are active and not marked deleted.
- **Filtered browse set**: The default browse set after optional name, price, and category search. With no search fields, this is the default browse set.
- **Base query**: Shared get-query rule in the building block. Every Catalog get query applies `IsActive = true` and `IsDeleted = false`.
- **Catalog search**: Get-by-id plus paginated browse, optionally filtered by name, price, or category.
- **Isolated check**: An automated check that exercises one unit of behavior without the log viewer stack and without the operator’s everyday catalog store.
- **Service-level check**: An automated check that drives Catalog through a public path against an isolated catalog store.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A client asking for one page of products receives only that page’s items plus the total count, and can use that information to ask for another page, in under 5 seconds on a typical local catalog of at least 25 products.
- **SC-002**: 100% of default page responses omit products marked deleted.
- **SC-003**: 100% of successful create-product actions store a product that has an identity, created time, last-changed time, is not deleted, and is active.
- **SC-004**: 100% of successful update-product actions keep the same identity and created time and move last-changed time forward.
- **SC-005**: 100% of get-by-id journeys for a missing or deleted product report not found.
- **SC-006**: 100% of Catalog get-by-id and paginated-browse journeys have an isolated automated check and a service-level automated check that fail if the journey stops returning the stored product, the not-found result, or the correct page slice, total, or name/price/category filtered set.
- **SC-010**: 100% of page responses that include a name, price, or category search omit products that do not match every supplied field.
- **SC-007**: A developer can complete the isolated checks for this feature in under 2 minutes without starting the log viewer stack.
- **SC-008**: 100% of previously passing create-product and shared logging checks still pass after this feature is added.
- **SC-009**: Two identical page requests against an unchanged catalog return the same products in the same order in 100% of runs.

## Assumptions

- Product constraints from the request: the reusable item base and page model live in the shared building block (`BuildingBlock`); the requested contract names are `BaseEntity`, `IAuditable`, `IDeletable`, and `IActivable`; Catalog Product uses `BaseEntity`. Those names are requested constraints, not success-criterion implementation steps.
- “Search Catalog.Api” means get-by-id plus paginated browse, optionally filtered by name (contains, case-insensitive), exact price, or category (exact, case-insensitive, against one listed category). Combined search fields are AND. Blank name or category is ignored.
- Public delete-product and activate/deactivate actions are out of scope. The deletion and activable traits must still exist so those actions can be added later. Isolated checks may mark a product deleted or inactive directly in the isolated store to prove default-page and get-by-id rules.
- Catalog has no authenticated caller today. Audit records created and last-changed times only. Actor fields may exist on the audit trait and stay empty until Catalog has caller identity.
- New products are active and not deleted. Created time and last-changed time are set on create. Update refreshes last-changed time only.
- Default page size is 10. Maximum page size is 100. Page numbering starts at 1. These defaults are for a consistent client contract; they are not success-criterion implementation steps.
- Default browse order is last-changed time, newest first, then identity. That keeps page results stable without requiring a client-supplied sort in v1.
- Every get query uses the shared base query: `IsActive` and not `IsDeleted`. Inactive or deleted items are not returned on browse or get-by-id.
- Get-by-id, create-product, and update-product public paths stay as they are. This feature adds the browse/page path and shared traits; it does not rename existing product fields (name, price, description, image file, categories).
- Everyday automated Catalog checks keep using an isolated store. The local compose catalog store from the hosting feature is not required to accept this feature.
- Existing create-product isolated and service-level checks from the Catalog testing feature stay and still pass. This feature adds get-by-id and page coverage; it does not rewrite create-product tests unless a shared-trait assertion must be added without changing create-product’s public path.
- The project constitution file is still a placeholder and adds no extra governance constraints beyond the existing spec-driven workflow.
- Git branch name and spec directory name are independent. This specification lives under `005-catalog-page-entities` regardless of the current working branch.
