# Feature Specification: Catalog and BuildingBlocks Automated Tests

**Feature Branch**: `003-catalog-buildingblocks-tests`

**Created**: 2026-10-27

**Status**: Draft

**Input**: User description: "Catalog.Api + BuildingBlocks unit and integration tests"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Prove Catalog can create a product (Priority: P1)

A developer or reviewer needs confidence that Catalog’s existing create-product behavior works. They run isolated automated checks that exercise create-product without starting the log viewer stack. They also run service-level automated checks that send a create-product request through Catalog’s public create path against an isolated catalog store. Both sets of checks fail if create-product stops returning a new product identity or stops storing the product.

**Why this priority**: Catalog currently has no automated checks for create-product. Until those exist, regressions in the only public Catalog product action go unnoticed. This is the minimum viable capability.

**Independent Test**: Run the Catalog isolated checks and the Catalog service-level checks with the log viewer stack stopped. A valid create-product action returns a new identity, the product is stored, and the checks complete without Grafana or Loki.

**Acceptance Scenarios**:

1. **Given** Catalog’s create-product logic and an isolated catalog store, **When** a developer runs the isolated automated checks, **Then** a valid create-product action returns a new product identity and the stored product matches the submitted name, price, description, image file, and categories.
2. **Given** Catalog is started for service-level checks against an isolated catalog store, **When** a client submits a valid create-product request on the existing public create path, **Then** the response identifies a created product and that product is retrievable from the store by that identity.
3. **Given** the log viewer stack is not running, **When** a developer runs those Catalog checks, **Then** they still complete and do not require activating Loki in Grafana.
4. **Given** create-product is broken so that it no longer returns an identity or no longer stores the product, **When** the same checks run, **Then** they fail.

---

### User Story 2 - Prove the shared building block Catalog relies on (Priority: P2)

A developer adding or changing the shared building block that Catalog uses for command handling and public request entry points needs automated checks that this building block can be enabled and used. They run isolated automated checks for that building block without standing up Catalog or the log viewer stack. Later services can follow the same pattern without copying Catalog’s create-product checks.

**Why this priority**: Catalog cannot create products if the shared building block fails to register handlers or HTTP modules. Those checks are independent of create-product itself, but they protect every service that reuses the block.

**Independent Test**: Run only the shared-building-block isolated checks. They pass when the block can be enabled and used to dispatch a sample command and to register public request entry points. Catalog and the log viewer stack are not required.

**Acceptance Scenarios**:

1. **Given** the untested shared building block that Catalog already uses, **When** a developer runs its isolated automated checks, **Then** enabling the block succeeds and a sample command can be dispatched through the shared command pattern.
2. **Given** the same building block’s request-entry registration, **When** the isolated checks run, **Then** they prove Catalog’s public request entry points can be registered without each service inventing its own registration.
3. **Given** the existing shared logging test suite, **When** this feature is complete, **Then** that logging suite still exists and still passes; this feature does not replace or rewrite it.
4. **Given** a later service that enables the same shared building block, **When** a developer looks at these checks, **Then** they can reuse the building-block check pattern without copying Catalog create-product scenarios.

---

### User Story 3 - Run the suites independently and keep existing diagnostics checks green (Priority: P3)

A developer on a machine without the local log viewer stack still needs to prove Catalog create-product and the shared building block. They can run isolated checks quickly, then optionally run service-level Catalog checks against an isolated store. Existing diagnostic-export checks from the logging feature still pass. Completing this work does not change how Catalog writes or exports diagnostics, and does not change Loki activation.

**Why this priority**: Suites that secretly depend on Grafana would make “Catalog is tested” look broken on a fresh clone. Independence is required for daily use, but it is not the first create-product proof.

**Independent Test**: With Grafana stopped, run isolated checks in under two minutes, then run Catalog service-level checks against an isolated store. Re-run the existing logging test suite and confirm it still passes.

**Acceptance Scenarios**:

1. **Given** Grafana and Loki are stopped, **When** a developer runs the isolated Catalog and building-block checks, **Then** those checks finish without starting the log viewer stack.
2. **Given** a missing or empty catalog store setting, **When** Catalog is started for service-level checks, **Then** startup fails with a clear error that names the missing store setting (the same rule Catalog already uses in normal operation).
3. **Given** the existing shared logging tests, **When** the new Catalog and building-block checks are added, **Then** 100% of the previously passing logging checks still pass.
4. **Given** this feature is complete, **When** an operator starts the local log viewer stack, **Then** Loki activation and Catalog diagnostic export behave as they did before (no change required).

---

### Edge Cases

- Isolated Catalog checks must not require a live operator catalog database; they use an isolated store or in-suite substitute for create-product persistence.
- Service-level checks must not write to a shared developer catalog store used outside the suite; each run uses an isolated store.
- Create-product with empty name, negative price, or missing categories: this feature records current Catalog behavior with automated checks. It does not add new rejection rules unless Catalog already rejects those inputs.
- Two create-product actions in the same run receive different identities.
- The shared logging building block already has a test suite; this feature must not duplicate those diagnostic-export scenarios.
- Activating Loki in Grafana (feature 002) stays out of this feature. Catalog checks must not start or configure that stack.
- Catalog product operations that do not exist yet (read, update, delete) are out of scope.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Catalog MUST have isolated automated checks that prove a valid create-product action returns a new product identity and stores name, price, description, image file, and categories.
- **FR-002**: Catalog MUST have service-level automated checks that submit a valid create-product request on the existing public create path and prove the created identity corresponds to a stored product.
- **FR-003**: The untested shared building block that Catalog uses for command handling MUST have isolated automated checks that prove it can be enabled and that a sample command can be dispatched through the shared command handling.
- **FR-004**: The shared building block’s request-entry registration MUST have isolated automated checks that prove Catalog’s public request entry points can be registered without each service inventing its own registration.
- **FR-005**: Isolated Catalog and building-block checks MUST complete without the local log viewer stack (Grafana/Loki) running.
- **FR-006**: Service-level Catalog checks MUST use an isolated catalog store, not the operator’s everyday local catalog database.
- **FR-007**: When the catalog store setting is missing or empty, Catalog startup used by service-level checks MUST fail with a clear error that names the missing setting.
- **FR-008**: This feature MUST NOT replace, remove, or rewrite the existing shared logging test suite.
- **FR-009**: This feature MUST NOT change how Catalog writes or exports diagnostic records, and MUST NOT change Loki activation in Grafana.
- **FR-010**: Create-product checks MUST fail if the action stops returning a new identity or stops storing the product.
- **FR-011**: Two successful create-product actions in one check run MUST receive distinct identities.
- **FR-012**: This feature MUST cover Catalog’s existing create-product behavior only; it MUST NOT require new Catalog product operations (read, update, delete) as a completion condition.

### Key Entities

- **Product**: A catalog item with identity, name, price, description, image file, and categories.
- **Create-product action**: The existing Catalog operation that accepts product fields and returns a new identity after storing the product.
- **Isolated check**: An automated check that exercises one unit of behavior without the log viewer stack and without the operator’s everyday catalog database.
- **Service-level check**: An automated check that drives Catalog through its public create-product path against an isolated catalog store.
- **Shared building block**: The untested shared helpers Catalog already uses for command handling and public request entry points (not the shared logging building block, which already has checks).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of Catalog’s existing create-product journeys have an isolated automated check and a service-level automated check that fail if create-product stops returning a new identity or stops storing the product.
- **SC-002**: A developer can complete the isolated Catalog and building-block checks in under 2 minutes without starting the log viewer stack.
- **SC-003**: A developer can complete the Catalog service-level create-product checks against an isolated store in under 5 minutes on a typical local workstation.
- **SC-004**: 100% of previously passing shared logging checks still pass after this feature is added.
- **SC-005**: At least 90% of developers following the documented local steps can run the isolated checks on the first attempt without Grafana.
- **SC-006**: Two create-product actions in the same service-level run always produce different identities (100% of runs).

## Assumptions

- Product constraint from the request: automated coverage is both isolated (unit) checks and service-level (integration) checks for Catalog.Api and BuildingBlocks. Those names are constraints, not implementation steps.
- “BuildingBlocks” in this feature means the untested shared building block Catalog already uses for command handling and public request entry points. The shared logging building block already has a test suite from the logging feature; that suite stays and is not rewritten here.
- Catalog’s only existing public product operation is create-product. Read, update, and delete are out of scope until those operations exist.
- Service-level checks use an isolated catalog store provisioned for the suite. They do not require Grafana, Loki, or a change to Catalog diagnostic export.
- Isolated checks may use an isolated store or in-suite substitute for persistence; they must still prove the product is stored, not only that create-product was invoked.
- Missing catalog store configuration already fails Catalog startup with a named setting error; service-level checks reuse that rule.
- This feature does not add new create-product validation rules. Checks describe current behavior for invalid or empty fields unless Catalog already rejects them.
- Loki activation in Grafana remains the completed observability feature and is out of scope here.
- Git branch name and spec directory name are independent. This specification lives under `003-catalog-buildingblocks-tests` regardless of the current working branch.
