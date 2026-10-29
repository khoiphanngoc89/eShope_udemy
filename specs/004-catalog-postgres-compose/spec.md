# Feature Specification: Host Local Catalog Store and Catalog.Api

**Feature Branch**: `004-catalog-postgres-compose`

**Created**: 2026-10-29

**Status**: Draft

**Input**: User description: "help me to write docker compose for host the postgreSQL for catalog api"

## Clarifications

### Session 2026-10-29

- Q: Once Catalog.Api is added to the local compose stack, which run path must this feature prove as the official way to use Catalog against the local catalog store? → A: Compose is the documented run path: Catalog.Api is a service in the same compose pair; success is starting that Catalog container with the catalog store and completing a valid create-product. Host Catalog is not part of this feature’s acceptance.
- Q: When a developer starts Catalog.Api from compose, how must Catalog and the catalog store relate at startup? → A: Documented Catalog run starts Catalog.Api with the catalog store; Catalog is ready only after the store is ready. Developers may still start only the catalog store. The log viewer is not required. If the store is stopped, Catalog must not appear available.
- Q: Which catalog-store address should the docs list for Catalog.Api compared with a developer connecting from their own machine? → A: Two views of the same store: Catalog.Api uses the in-stack store address; a developer on the host uses the published local store address. Same database name and local credentials.
- Q: From the developer’s machine, how should a client reach Catalog.Api to create a product? → A: Catalog.Api is published on a documented local HTTP URL on the developer machine. HTTPS is not required for this local feature.
- Q: When Catalog.Api runs in compose, should it send diagnostic records to the local log viewer, and must Catalog still start if that log viewer is stopped? → A: When the log viewer is running, Catalog.Api sends diagnostics to it using the in-stack log-viewer address. Catalog.Api still starts and accepts create-product if the log viewer is stopped. Do not change Catalog’s public create-product path.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Start a local catalog store for Catalog (Priority: P1)

A developer who wants to run Catalog locally starts the catalog store from the project’s single compose pair with one documented command. They wait until the store reports ready. They do not install a database server on the machine by hand. They can start only the catalog store service; Grafana is in the same files but is not required for the store.

**Why this priority**: Catalog already refuses to start without a catalog store setting. Until a local store can be started and marked ready, no other story in this feature delivers value.

**Independent Test**: Run only the documented catalog-store start command (`catalogdb` service). Confirm the store becomes ready and accepts a connection using the documented local connection details. Grafana/Loki may stay stopped.

**Acceptance Scenarios**:

1. **Given** a developer machine with the project’s usual local container runtime available, **When** the developer runs the documented catalog-store start command, **Then** a dedicated catalog store starts and reaches a ready state without requiring the log viewer service to be running.
2. **Given** the catalog store has reached a ready state, **When** the developer uses the documented published local store address with the documented database name and local credentials, **Then** they can open a connection to the catalog store from the host.
3. **Given** the catalog store is starting for the first time on that machine, **When** the start command finishes successfully, **Then** an empty catalog database exists and is ready for Catalog to use.

---

### User Story 2 - Run Catalog.Api from the same compose pair (Priority: P2)

A developer starts Catalog.Api from the same compose pair as the catalog store. That documented Catalog run starts the catalog store if it is not already running. Catalog becomes ready only after the store is ready, with the documented local catalog store setting already applied. From the developer machine they call Catalog on the documented local HTTP URL and perform a valid create-product action. The product is stored in the local catalog store and can be found there after the action completes. This feature does not require running Catalog as a host process. HTTPS is not required. The log viewer is not required; when it is running, Catalog diagnostic records go to it.

**Why this priority**: Hosting a store is only useful if Catalog can use it. This story depends on the store being startable (P1) and is the proof that the feature serves Catalog rather than an unused database.

**Independent Test**: Run the documented Catalog.Api start command (same compose pair). Confirm the catalog store starts if needed, Catalog becomes ready only after the store is ready, and one valid create-product action against the documented local HTTP URL persists in the local store. Do not start Catalog as a host process. HTTPS and the log viewer are not required.

**Acceptance Scenarios**:

1. **Given** a developer machine with the catalog store not yet running, **When** the developer runs the documented Catalog.Api start command, **Then** the catalog store starts, Catalog becomes ready only after the store is ready, and Catalog has the documented local catalog store setting already applied.
2. **Given** Catalog.Api is running as that compose service against the local store, **When** a client on the developer machine submits a valid create-product request to the documented local HTTP URL on Catalog’s existing public create path, **Then** Catalog returns a new product identity and that product is stored in the local catalog store.
3. **Given** the documented local Catalog run is the Catalog.Api compose service, **When** the developer inspects the documented local steps, **Then** they can see that Catalog.Api is given the in-stack store address (same database name and local credentials as the host view) without asking a teammate and without running Catalog on the host.
4. **Given** Catalog.Api is running and the log viewer is also running, **When** a client submits a valid create-product request, **Then** Catalog diagnostic records for that action are sent to the log viewer using the in-stack log-viewer address, and Catalog’s public create-product path is unchanged.

---

### User Story 3 - Restart, stop, and recognize an unready store (Priority: P3)

A developer stops or restarts the local catalog store during a work session. Catalog data created earlier in that local store is still there after a successful restart. If the store is stopped or still starting, the developer can tell it is not ready from the documented status, and Catalog does not silently pretend the store is available.

**Why this priority**: Local stores are started and stopped often. Losing local catalog data on every restart, or failing with no explanation, would make the hosted store unusable even when start and create-product already work.

**Independent Test**: Create a product in the local store, restart only the catalog store, and confirm the product is still present. Separately, stop the store and confirm documented status shows it is not ready.

**Acceptance Scenarios**:

1. **Given** at least one product exists in the local catalog store, **When** the developer restarts the catalog store with the documented commands, **Then** that product is still present after the store is ready again.
2. **Given** the catalog store is stopped or still starting, **When** the developer checks the documented status, **Then** they can tell the store is not ready without guessing from a blank failure.
3. **Given** the catalog store is stopped or not yet ready, **When** Catalog.Api is started or already running, **Then** Catalog does not appear available until the store is ready; if the store never becomes ready, Catalog does not start successfully as if the store were available, and the failure is attributable to the store being unreachable.
4. **Given** the catalog store is running, **When** the developer runs the documented stop command, **Then** the store stops and no longer accepts new connections.

---

### Edge Cases

- First start on a machine: the store becomes ready with an empty catalog database; the developer is not asked to create the database by hand.
- Starting Catalog.Api starts the catalog store if it is not already running; Catalog is not ready until the store is ready.
- Developers can still start only the catalog store without starting Catalog.Api.
- The store is still starting when Catalog.Api is started: Catalog does not appear healthy against a store that is not ready.
- If the store is stopped while Catalog.Api is running, Catalog does not appear available.
- The log viewer service is stopped: the catalog store still starts, and Catalog.Api still starts and accepts create-product. Starting Catalog.Api MUST NOT require the log viewer. Diagnostic records are not required to reach the log viewer in that case.
- The log viewer service is already running from the same compose pair: starting the catalog store or Catalog.Api does not require restarting or reconfiguring the log viewer. Catalog.Api sends diagnostic records to it using the in-stack log-viewer address.
- Everyday Catalog automated checks that use an isolated store continue to run without this local catalog store and without the Catalog.Api compose service.
- Host Catalog is not a required proof for this feature; the documented Catalog run is the Catalog.Api compose service.
- A second start while the store is already running does not create a conflicting second catalog store for the same local setup.
- Local default credentials are for local use only; they are not production secrets.
- Catalog.Api uses the in-stack store address, not the published host address, as its catalog store setting. A developer on the host uses the published local store address, not the in-stack address, to open a direct store connection.
- Stopping the store does not delete stored local catalog data; only an explicit documented reset would wipe it (reset is out of scope unless needed to recover a corrupt local store).
- Catalog product operations that do not exist yet (read, update, delete as public APIs) are not required to complete this feature; create-product plus presence in the store is sufficient proof.
- HTTPS is not required to reach Catalog.Api for this local feature; the documented client URL is HTTP only.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Developers MUST be able to start a dedicated local catalog store with a single documented command.
- **FR-002**: Developers MUST be able to stop that local catalog store with a single documented command.
- **FR-003**: The local catalog store MUST reach a documented ready state before it is considered available for Catalog.
- **FR-004**: The local catalog store MUST be a PostgreSQL database suitable for Catalog’s existing catalog store setting (product constraint from the request and from Catalog’s current store requirement).
- **FR-005**: The local catalog store MUST be hosted as a developer-startable compose stack so the same start/stop workflow can be repeated on a fresh machine (product constraint from the request).
- **FR-006**: Documentation MUST list two views of the same local catalog store: the in-stack store address used by the Catalog.Api compose service, and the published local store address a developer uses from the host. Database name and local credentials MUST be the same in both views. Filling a host Catalog settings file is not the documented success path.
- **FR-007**: When the Catalog.Api compose service is started with the documented in-stack store address (same database name and local credentials as the host view) and the store is ready, Catalog MUST become ready only after the store is ready, and MUST start successfully once the store is ready.
- **FR-008**: A valid create-product action against Catalog running on that local store MUST persist the product in the local catalog store under the returned identity.
- **FR-009**: Catalog data stored in the local catalog store MUST still be present after the store is restarted.
- **FR-010**: Starting the catalog store or Catalog.Api MUST NOT require the local log viewer service to be running. Grafana/Loki live in the same compose pair; the operator MAY start only `catalogdb`. Starting Catalog.Api MAY start the catalog store; it MUST NOT require starting the log viewer.
- **FR-011**: This feature MUST NOT change Catalog’s public create-product behavior or request path. Diagnostic record shape stays the same.
- **FR-012**: Existing Catalog automated checks that use an isolated store MUST continue to run without this local catalog store.
- **FR-013**: Local store credentials and connection details MUST be clearly identified as local-development defaults, not production secrets.
- **FR-014**: This feature MUST NOT host, replace, or require a production or shared cloud catalog database.
- **FR-015**: Catalog.Api MUST be a service in the same compose pair as the local catalog store. The documented way to run Catalog locally is that compose service. Running Catalog as a host process is not required to accept this feature.
- **FR-016**: The documented Catalog.Api start command MUST start the catalog store if it is not already running. Catalog MUST NOT appear available until the store is ready. Developers MUST still be able to start only the catalog store without starting Catalog.Api. If the store is stopped or never becomes ready, Catalog MUST NOT appear available; the failure MUST be attributable to the store.
- **FR-017**: Catalog.Api MUST be reachable from the developer machine on a documented local HTTP URL. HTTPS is not required for this local feature.
- **FR-018**: When the log viewer is running, the Catalog.Api compose service MUST send diagnostic records to it using the in-stack log-viewer address. Catalog.Api MUST still start and accept create-product if the log viewer is stopped.

### Key Entities

- **Local catalog store**: The dedicated local database that holds Catalog products for everyday developer runs of Catalog.
- **Catalog store setting**: The existing Catalog configuration value that must be present for Catalog to start and that points Catalog at a catalog store.
- **Catalog**: The catalog service (Catalog.Api) whose create-product action stores products when the catalog store setting is valid.
- **Catalog compose service**: Catalog.Api running as a service in the same compose pair as the local catalog store. This is the documented local Catalog runtime for this feature.
- **Catalog local URL**: The documented HTTP URL on the developer machine used to call Catalog.Api (create-product). HTTPS is not part of this URL.
- **Ready state**: For the store, the condition in which it accepts connections. For Catalog.Api, the condition in which the store is ready and Catalog accepts create-product requests. Catalog MUST NOT be considered ready while the store is not.
- **Local connection details**: Two views of the same store. **In-stack store address**: how Catalog.Api reaches the catalog store on the compose network. **Published local store address**: how a developer on the host reaches that same store. Database name and local credentials are shared; the host/port differ by view.
- **In-stack log-viewer address**: How Catalog.Api reaches the log viewer on the compose network when that viewer is running.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer following the documented steps can start the local catalog store and see it ready in under 2 minutes on a typical workstation after the first one-time setup wait.
- **SC-002**: 100% of successful documented Catalog starts that use the documented local store setting reach a running state only when the local catalog store is ready.
- **SC-003**: 100% of valid create-product actions sent to the documented local HTTP URL of Catalog using that local store result in a stored product that can be found in the store by the returned identity.
- **SC-004**: After a restart of the local catalog store, 100% of previously stored local catalog products from that store are still present.
- **SC-005**: At least 90% of developers following the documented local steps can start the store and Catalog on the first attempt without starting the log viewer service and without running Catalog as a host process.
- **SC-006**: 100% of previously passing Catalog isolated and service-level automated checks still pass without requiring the local catalog store to be running.
- **SC-007**: When the log viewer is running, Catalog diagnostic records for a create-product action are available there. When the log viewer is stopped, 100% of documented Catalog starts still succeed and accept create-product.

## Assumptions

- Product constraint from the request: the local catalog store is PostgreSQL, started and stopped through a local Docker Compose (or equivalent compose) definition. Those names are requested constraints, not success-criterion implementation steps.
- Scope is local developer use of Catalog. Production, staging, and shared cloud databases are out of scope.
- The catalog store, Catalog.Api, and the log viewer are services in one compose pair (`deploy/compose.yaml` + `deploy/compose.override.yaml`). A developer can run Catalog persistence without Grafana or Loki. Starting only `catalogdb` remains valid for the store (P1). The documented Catalog run starts Catalog.Api and the catalog store (store starts with Catalog if needed); Catalog is ready only after the store is ready. That run is the Catalog.Api compose service, not a host Catalog process.
- Catalog already requires a non-empty catalog store setting (`ConnectionStrings:CatalogDb`) at startup; this feature supplies a local store and applies the documented in-stack value to the Catalog.Api compose service. It does not invent a new Catalog configuration key. Host Catalog settings files are not the documented success path. The published host view of the same store remains `Host=localhost;Port=5432;Database=catalog;Username=catalog;Password=catalog` (local defaults only).
- Everyday automated Catalog checks from the Catalog testing feature keep using an isolated store. This local store is for running Catalog as a developer, not for replacing those checks.
- Create-product is sufficient to prove Catalog can persist to the local store. New Catalog read/update/delete public operations are out of scope.
- Local default credentials are acceptable for a developer workstation. They must not be treated as production secrets and must not be copied into production configuration.
- The documented local HTTP URL reuses Catalog’s existing local URL (`http://localhost:5000`) so existing create-product clients keep working. HTTPS is out of scope for this local feature.
- When the log viewer is running, Catalog.Api sends diagnostics to the in-stack OpenTelemetry HTTP endpoint (`http://lgtm:4318`). When the log viewer is stopped, Catalog.Api still starts (same rule as the existing logging feature: an unreachable collector must not block the service). Diagnostic record shape is unchanged. Host Catalog export to `http://localhost:4318` is not this feature’s acceptance path.
- The developer machine already has a container runtime that can run the project’s existing local log viewer stack; this feature does not introduce a new runtime requirement.
- Data in the local catalog store is developer data. It persists across ordinary stop/start cycles. A documented factory-reset of local data is not required for v1.
- The project constitution file is still a placeholder and adds no extra governance constraints beyond the existing spec-driven workflow.
- Git branch name and spec directory name are independent. This specification lives under `004-catalog-postgres-compose` regardless of the current working branch.
