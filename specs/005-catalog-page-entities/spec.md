# Feature Specification: Paginated Catalog Browse and Shared Item Traits

**Feature Branch**: `005-catalog-page-entities`

**Created**: 2026-10-31

**Status**: Draft

**Input**: User description: "implement get product pagination and add missing unit tests for all Catalog.Api search; put the core model in BuildingBlock; declare IAuditable, IDeletable, and IActivable; implement BaseEntity; Product inherits from BaseEntity; clients can search the product page by name, price, or category"

## Clarifications

### Session 2026-10-31

- Q: What must a failed Catalog request log so Grafana can show where it broke? → A: Request name, duration, and the full stack trace in the log message. HTTP 500 stays a problem response without a stack.
- Q: How must product-page reads talk to Marten 9? → A: Asynchronous data access only. Sync `ToList()` on a Marten query is a defect.

### Session 2026-11-01

- Q: When a client asks for a product page or a name/price/category search, and the search index is unavailable, what should Catalog do? → A: Fail the page/search request with a clear service error. Do not return an empty success page and do not silently fall back to the catalog store. Get-by-id, create, and update still use the catalog store.
- Q: After a product is created or updated in the catalog store, how soon must that change show up in product-page and search results? → A: Within 5 seconds under normal sync. Writes return as soon as the catalog store commits. Debezium captures the change, Kafka carries it, and the indexer updates the search index.
- Q: When search sync starts, how should products that already exist in the catalog store get into the search index? → A: Snapshot existing products into the search index, then stream later catalog-store changes.
- Q: What prefix should every catalog search-sync topic use so operators can find those topics quickly? → A: `eshop.catalog.sync`
- Q: Which logs and metrics must product search and search sync expose so an operator can see outages and lag? → A: Failed-request logs plus search latency, search errors, sync lag, and sync/indexer failure logs. No success log on every snapshot or streamed product change.
- Q: After Catalog handles a request with the log viewer stack running, what must an operator see in Grafana Tempo? → A: Tempo Explore shows a Catalog request trace (waterfall) after traffic. A Tempo service graph is not required.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browse a page of catalog products (Priority: P1)

A catalog client needs to see products without loading the whole catalog at once. They ask for one page of products using a page number and a page size. They may also narrow that page by name, price, or category. Catalog serves that page and those filters from the search index and returns only that page’s matching products plus enough page information to know how many matches exist and how to ask for another page. An empty catalog, no matches, or a page past the last match returns an empty page rather than a failure. If the search index is unavailable, the page or search request fails with a clear service error instead of pretending the catalog is empty or reading the catalog store.

**Why this priority**: Catalog can create, fetch one product, and update a product, but it cannot yet return a bounded, filterable list. Until a client can page and search products, “search Catalog” is only get-by-id.

**Independent Test**: Create several products in an isolated catalog store. Wait up to 5 seconds for normal search sync. Request page 1 with a page size smaller than the total. Confirm the response contains only that many products and reports the total count. Request a later empty page and confirm it is empty, not an error.

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
10. **Given** the search index is unavailable, **When** a client asks for a product page or a name/price/category search, **Then** Catalog rejects the request with a clear service error and does not return an empty success page or results from the catalog store.
11. **Given** the search index is unavailable, **When** a client fetches a stored product by identity, **Then** Catalog still returns that product from the catalog store.
12. **Given** a successful create-product or update-product, **When** the response is returned, **Then** that response is based on the catalog-store commit and does not wait for the search index.
13. **Given** a successful create-product or update-product and normal search sync, **When** up to 5 seconds have passed, **Then** the product-page and name/price/category search results include that committed change.

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
6. **Given** a product is created or updated through a public path, **When** service-level page checks run, **Then** they wait up to 5 seconds for normal search sync before asserting the page or search result.

---

### User Story 4 - Keep search in step with catalog writes (Priority: P2)

An operator or client changes a product in the catalog store. Create and update succeed as soon as that store commit finishes. When search sync starts, it first snapshots products that already exist, then streams later committed changes onto message topics that all start with `eshop.catalog.sync` and into the search index. An operator can open Kafka UI and filter those prefixed topics to trace a sync. They can open Kibana and inspect the search index documents for the same products. Under normal sync, the product-page and name/price/category search show the new or changed product within 5 seconds. The create or update path does not write the search index itself. Operators can see search latency, search errors, and how far search sync is behind, and they get a failure log when a request or the indexer fails. Successful snapshot and stream writes are not logged one-by-one. When the log viewer stack is running, an operator can open Grafana Tempo Explore after Catalog traffic and see that request’s Catalog trace (waterfall). A Tempo service graph is not required.

**Why this priority**: Browse and search now read the search index. Without a durable sync from the catalog store, a successful write would not become searchable, or the API would have to block on the index.

**Independent Test**: Create a product through the public create path and confirm the response returns before the product is required to appear in search. Wait up to 5 seconds, then confirm the product page and a name search include that product.

**Acceptance Scenarios**:

1. **Given** a valid create-product request, **When** the catalog store commit succeeds, **Then** Catalog returns success without waiting for the search index.
2. **Given** that created product and normal search sync, **When** up to 5 seconds have passed, **Then** the default product page and a matching name search include that product.
3. **Given** a valid update-product request, **When** the catalog store commit succeeds, **Then** Catalog returns success without waiting for the search index.
4. **Given** that updated product and normal search sync, **When** up to 5 seconds have passed, **Then** the product page and matching search show the new name, price, or category.
5. **Given** create-product or update-product, **When** the search index, message stream, or change-data capture path is down, **Then** the write still succeeds and get-by-id still returns the stored product.
6. **Given** products already exist in the catalog store and search sync starts, **When** the initial snapshot finishes, **Then** those existing active, not-deleted products are present in the search index without requiring an update.
7. **Given** the initial snapshot has finished, **When** a later create-product or update-product commits, **Then** that change is streamed to the search index and is visible under normal sync within 5 seconds.
8. **Given** search sync is running, **When** an operator opens Kafka UI and filters topics by `eshop.catalog.sync`, **Then** every catalog search-sync topic is listed under that prefix and no catalog search-sync topic uses another prefix.
9. **Given** search sync is running and the search index has products, **When** an operator opens Kibana on the documented local URL, **Then** they can inspect the catalog product search documents without starting Catalog as a host process.
10. **Given** a product-page or name/price/category search request fails, **When** an operator inspects logs, **Then** they see the existing failed-request log (request name, duration, and stack in the message).
11. **Given** product-page and search traffic, **When** an operator inspects metrics, **Then** they can read search latency and search error counts.
12. **Given** search sync is running or falling behind, **When** an operator inspects metrics, **Then** they can read sync lag.
13. **Given** the indexer or search-sync path fails, **When** an operator inspects logs, **Then** they see a sync/indexer failure log. They do not require a success log for every snapshot or streamed product.
14. **Given** the log viewer stack is running and Catalog has handled at least one public request, **When** an operator opens Grafana Tempo Explore, **Then** they see that request’s Catalog trace (waterfall). They do not require a Tempo service graph.

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
- A failed request’s Grafana/Loki error row must include the stack trace in the log message, not only the request name and duration.
- Product-page and name/price/category search are served from the search index. If that index is unavailable, the request is a clear service failure, not an empty success page and not a silent catalog-store fallback.
- Get-by-id, create-product, and update-product use the catalog store. Those paths must keep working when the search index is down.
- Create-product and update-product return after the catalog-store commit. They do not write the search index in the same request and do not wait for search sync.
- Under normal sync, a committed create or update is visible in product-page and name/price/category search within 5 seconds. Immediate same-request search visibility is not required.
- When search sync starts, existing catalog-store products are snapshotted into the search index first. Later changes are streamed. Products stored before sync started are not left missing until someone updates them.
- Every catalog search-sync topic name starts with `eshop.catalog.sync`. Kafka UI is available so an operator can filter and trace those topics.
- Kibana is available so an operator can inspect search-index documents. Catalog startup must not require Kibana.
- Failed Catalog requests keep the existing error log (request name, duration, stack in the message). Search also exposes latency and error metrics. Search sync exposes lag and a failure log when the indexer or sync path fails. A success log is not required for every snapshot or streamed product.
- When the log viewer stack is running, a public Catalog request MUST appear as a request trace (waterfall) in Grafana Tempo Explore. An empty Tempo after traffic is a defect. A Tempo service graph is not required. Catalog startup and `dotnet test` MUST NOT require Tempo.
- Catalog-store materialization against a live Marten store must be async. Sync `ToList()` fails on Marten 9 with “only asynchronous data access is supported.”
- This feature must not replace or rewrite the existing create-product or shared logging test suites.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Clients MUST be able to request one page of catalog products by page number and page size on a public Catalog browse path. That browse path MUST read the search index, not the catalog store.
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
- **FR-019**: Clients MUST be able to optionally search the product page by name, price, or category against the search index. A name match is case-insensitive contains. A price match is exact. A category match is a case-insensitive exact match against one listed category. When more than one search field is supplied, a product MUST match all of them.
- **FR-020**: Catalog MUST reject a negative price search with a clear validation failure. Omitted or blank name and category MUST mean no filter on that field.
- **FR-021**: When a Catalog request fails, the error log MUST include the request name, elapsed time, and the full stack trace in the log message so an operator can find the source in Grafana. The public HTTP 500 body MUST NOT include a stack trace.
- **FR-022**: Catalog-store reads that still use Marten (get-by-id and any supporting reads for create or update) MUST use asynchronous Marten data access. Synchronous enumeration of a Marten query is not allowed.
- **FR-023**: When the search index is unavailable, Catalog MUST fail the product-page and name/price/category search request with a clear service error. Catalog MUST NOT return an empty success page and MUST NOT silently query the catalog store for that request.
- **FR-024**: Get-by-id, create-product, and update-product MUST keep using the catalog store and MUST remain available when the search index is down. The catalog store remains the source of truth; the search index is a disposable read model for browse and search.
- **FR-025**: A successful create-product or update-product MUST return once the catalog-store commit succeeds. The response MUST NOT wait for the search index.
- **FR-026**: Under normal search sync, a committed create-product or update-product change MUST be visible in the product page and in name/price/category search within 5 seconds.
- **FR-027**: Catalog MUST copy committed catalog-store product changes to the search index through change-data capture. Requested path: Debezium publishes the change, Kafka carries it, and an indexer updates the search index. Create-product and update-product MUST NOT dual-write the search index.
- **FR-028**: When search sync starts, Catalog MUST first snapshot existing catalog-store products into the search index, then stream later committed changes. The snapshot MUST include active, not-deleted products already stored before sync started.
- **FR-029**: Every catalog search-sync topic name MUST start with the prefix `eshop.catalog.sync`. No catalog search-sync topic MAY use a different prefix.
- **FR-030**: Operators MUST be able to inspect catalog search-sync topics in Kafka UI by filtering on the `eshop.catalog.sync` prefix.
- **FR-033**: Operators MUST be able to inspect catalog product search documents in Kibana on a documented local HTTP URL. Catalog.Api, create-product, get-by-id, and `dotnet test` MUST NOT require Kibana to be running.
- **FR-031**: Product-page and name/price/category search MUST expose search latency and search error metrics. Failed search requests MUST still write the existing failed-request log from FR-021.
- **FR-032**: Search sync MUST expose a sync-lag metric and MUST write a failure log when the indexer or change-data-capture path fails. Catalog MUST NOT require a success log for every snapshot or streamed product change.
- **FR-034**: When the log viewer stack is running, Catalog MUST export a request trace for each public Catalog request so an operator can open Grafana Tempo Explore and see that request’s Catalog trace (waterfall) after traffic. A Tempo service graph is not required. Catalog.Api, create-product, get-by-id, browse, and `dotnet test` MUST NOT require Tempo to be running.

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
- **Catalog search**: Paginated browse plus optional name, price, or category filters, served from the search index. Get-by-id is a catalog-store read, not a search-index read.
- **Search index**: The disposable read model used for product-page and name/price/category search. Requested implementation: Elasticsearch. If it is unavailable, browse and search fail visibly.
- **Catalog store**: The source of truth for product identity and writes. Get-by-id, create-product, and update-product use it.
- **Search sync**: The change-data-capture path that copies catalog-store products to the search index. It starts with a snapshot of existing products, then streams later committed changes. Requested path: Debezium, then Kafka, then the indexer. Create and update do not write the index themselves.
- **Normal search sync**: Search sync is running and not failing. Under this condition, a committed product change is searchable within 5 seconds.
- **Search-sync topic**: A message topic used to snapshot or stream catalog product changes toward the search index. Every such topic starts with `eshop.catalog.sync`.
- **Kafka UI**: The operator console for browsing Kafka topics. Operators use it to find and trace catalog search-sync topics by the `eshop.catalog.sync` prefix.
- **Kibana**: The operator console for browsing the search index. Operators use it to inspect catalog product documents after snapshot or stream.
- **Search latency metric**: How long product-page and name/price/category search requests take.
- **Search error metric**: How many product-page and name/price/category search requests fail.
- **Sync lag metric**: How far search sync is behind the latest committed catalog-store product change.
- **Request trace**: The operator view of one Catalog request in Grafana Tempo Explore (waterfall of that request). Distinct from Loki log rows and from a Tempo service graph.
- **Tempo**: The operator console in the log viewer stack for request traces. Operators use Grafana Tempo Explore. Isolated and service-level `dotnet test` do not start Tempo.
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
- **SC-011**: 100% of product-page and name/price/category search requests fail with a clear service error when the search index is unavailable. None of those failed requests return an empty success page or results read from the catalog store.
- **SC-012**: 100% of get-by-id journeys for a stored, not-deleted, active product still succeed when the search index is unavailable.
- **SC-013**: 100% of successful create-product and update-product responses return after the catalog-store commit and do not wait for the search index.
- **SC-014**: Under normal search sync, 100% of committed create-product and update-product changes are visible in the product page and in a matching name, price, or category search within 5 seconds.
- **SC-015**: After search sync starts and the initial snapshot finishes, 100% of active, not-deleted products that already existed in the catalog store are present in the search index without requiring an update.
- **SC-016**: 100% of catalog search-sync topic names start with `eshop.catalog.sync`. An operator can list those topics by filtering on that prefix.
- **SC-020**: After search sync has indexed at least one product, an operator can open Kibana and see that product document in the catalog product index.
- **SC-017**: 100% of failed product-page and name/price/category search requests produce a failed-request log that includes the request name, duration, and stack in the message.
- **SC-018**: An operator can read current search latency, search error count, and search-sync lag without opening Kafka topic payloads.
- **SC-019**: 100% of indexer or search-sync path failures produce a failure log. Successful snapshot and stream writes do not require a per-product success log.
- **SC-021**: After Catalog handles at least one public request with the log viewer stack running, an operator can open Grafana Tempo Explore and see that request’s Catalog trace. A Tempo service graph is not required.

## Assumptions

- Product constraints from the request: the reusable item base, page model, and Elasticsearch search store live in the shared building block (`BuildingBlock`); the requested contract names are `BaseEntity`, `IAuditable`, `IDeletable`, and `IActivable`; Catalog Product uses `BaseEntity` and `ISearchStore<Product>`. Those names are requested constraints, not success-criterion implementation steps.
- “Search Catalog.Api” means paginated browse plus optional name (contains, case-insensitive), exact price, or category (exact, case-insensitive, against one listed category), served from the search index. Combined search fields are AND. Blank name or category is ignored. Get-by-id is a catalog-store read, not part of the search-index path.
- The catalog store is the source of truth. The search index is a disposable read model (requested: Elasticsearch). A search outage must be visible: page and search fail with a service error. Silent fallback to the catalog store is rejected because it hides the outage and can return a different match set than the index.
- Get-by-id, create-product, and update-product public paths stay on the catalog store and must not be blocked by a search-index outage.
- Search freshness is eventually consistent. Requested sync path: Debezium captures catalog-store product changes, Kafka carries them, and an indexer updates Elasticsearch. The write API does not dual-write the index. Under normal search sync, the freshness budget is 5 seconds.
- Search sync is snapshot-then-stream. Existing catalog-store products are loaded once when sync starts; later creates and updates are streamed. Stream-only startup is out of scope because it would hide products stored before sync.
- All catalog search-sync topics use the prefix `eshop.catalog.sync`. Kafka UI is in scope so operators can filter and trace those topics. Unrelated Kafka topics are not renamed.
- Kibana is in scope as the search-index operator UI (requested). Local Kibana has no auth, same class of local-dev default as Kafka UI. Isolated and service-level `dotnet test` do not start Kibana.
- Observability for this feature reuses the existing failed-request log and adds search latency, search errors, sync lag, sync/indexer failure logs, and a Catalog request trace in Grafana Tempo Explore when the log viewer stack is running. Per-product success logs for snapshot or stream writes are out of scope. A Tempo service graph is out of scope. Isolated checks may assert logs and metrics without starting the log viewer stack.
- Public delete-product and activate/deactivate actions are out of scope. The deletion and activable traits must still exist so those actions can be added later. Isolated checks may mark a product deleted or inactive directly in the isolated store to prove default-page and get-by-id rules.
- Catalog has no authenticated caller today. Audit records created and last-changed times only. Actor fields may exist on the audit trait and stay empty until Catalog has caller identity.
- New products are active and not deleted. Created time and last-changed time are set on create. Update refreshes last-changed time only.
- Default page size is 10. Maximum page size is 100. Page numbering starts at 1. These defaults are for a consistent client contract; they are not success-criterion implementation steps.
- Default browse order is last-changed time, newest first, then identity. That keeps page results stable without requiring a client-supplied sort in v1.
- Every get query uses the shared base query: `IsActive` and not `IsDeleted`. Inactive or deleted items are not returned on browse or get-by-id.
- This feature adds the browse/page path and shared traits; it does not rename existing product fields (name, price, description, image file, categories).
- Everyday automated Catalog checks keep using an isolated store. The local compose catalog store from the hosting feature is not required to accept this feature.
- Existing create-product isolated and service-level checks from the Catalog testing feature stay and still pass. This feature adds get-by-id and page coverage; it does not rewrite create-product tests unless a shared-trait assertion must be added without changing create-product’s public path.
- The project constitution file is still a placeholder and adds no extra governance constraints beyond the existing spec-driven workflow.
- Git branch name and spec directory name are independent. This specification lives under `005-catalog-page-entities` regardless of the current working branch.
