# Feature Specification: All Container Logs Without App Duplicates

**Feature Branch**: `008-all-container-logs`

**Created**: 2026-11-01

**Status**: Draft

**Input**: User description: "all containers log I want also sync them to grafana, however, I dont want to make application logging duplicate with container log"

## Clarifications

### Session 2026-11-01

- Q: Besides the store’s process lines in the log view, which database signals should Grafana show? → A: Both: store process logs in the log view, plus store health metrics on Ops/Infra (up, connections, size). Not SQL text or per-query payloads.
- Q: When an operator opens Grafana to check the stack, should they see one overview of every container as healthy or not, then click a container to see its detail metrics? → A: One overview in the existing Grafana (evolve Ops/Infra): every stack container shows healthy or not; click a container to open its detail metrics. Log search stays the existing log view.
- Q: For a container on that overview, what should “healthy” mean? → A: Healthy = process is up. Stopped or missing = not healthy. Empty data (for example an empty catalog) is not an outage. Detail metrics stay richer where they already exist.
- Q: Which containers must always appear on that Ops/Infra overview so a missing one shows as not healthy? → A: Product stack only: store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, and metric exporters. Collector and Grafana are not required on the overview.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See every stack container’s process logs in Grafana (Priority: P1)

An operator starts the local stack and the existing log viewer. They open the same Grafana they already use, pick the existing log view, and can filter to any stack container that only writes process output (store, broker, connector, search index, operator UIs, metric exporters, and similar). Those lines arrive because container or process output was shipped to the viewer — not because the operator opened a second log product or pasted a file.

**Why this priority**: The operator asked to sync **all** container logs. Until they can find stdout from the rest of the stack (not only Catalog and the Kafka broker), this feature does not deliver the requested coverage.

**Independent Test**: Start the documented local stack and log viewer. After each target container is healthy, search the last 15 minutes for that container’s identity. Process lines appear without adding a data source. Kafka topic message bodies are not the primary result.

**Acceptance Scenarios**:

1. **Given** Grafana and the log collection path have started successfully, **When** an operator opens the existing log view, **Then** they can search without adding a data source by hand.
2. **Given** the store, Kafka broker, connector, search index, and operator UIs have written recent process output, **When** the operator filters the last 15 minutes by each of those identities, **Then** matching process lines appear (store process lines are required; **healthy/not and detail metrics** are User Story 1b).
3. **Given** a first start with a container that has not yet written anything, **When** the operator runs the same search, **Then** they see a clear empty result, not a configuration or connection error.
4. **Given** products are flowing on sync topics, **When** the operator uses this log path for the Kafka broker, **Then** they see broker process text, not topic message bodies (the topic UI remains the place for payloads).

---

### User Story 1b - See every container healthy or not, then open detail metrics (Priority: P1)

An operator opens the existing Ops/Infra view. They see **one overview** of the **product stack**: store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, and metric exporters. Each is marked healthy or not. **Healthy** means that container’s process is up. Stopped or missing is not healthy. Empty data (for example an empty catalog) is not an outage. The log collector and Grafana are not required on this board. They click a container and see that container’s **detail metrics** in the same Grafana. For the catalog store, detail includes up, connections, and size. Search-index, Kafka, and Catalog.Api keep the richer signals they already have. Other product-stack containers may show only up. They do not read SQL text or per-query payloads. They do not open a second Grafana. Process-line search stays the existing log view.

**Why this priority**: The operator asked for one board that shows whether each container is healthy, then detail metrics on click. Process lines (US1) tell what a container printed; this overview tells whether its process is up.

**Independent Test**: Start the documented local stack including Grafana. Open Ops/Infra. After about one minute, the overview shows healthy or not for each product-stack container (store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, exporters). A product-stack container that was not started shows as not healthy. Click the store (and at least one other container) to see detail metrics. Catalog create and get-by-id still work if that metrics path is stopped. The log collector and Grafana need not appear on the board.

**Acceptance Scenarios**:

1. **Given** Grafana has started successfully, **When** an operator opens Ops/Infra, **Then** they see one overview of the product stack (store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, metric exporters) as healthy (process up) or not (stopped or missing), without adding a metrics data source by hand.
2. **Given** the overview is open, **When** the operator clicks a container, **Then** they see that container’s detail metrics in the same Grafana (store detail includes up, connections, and size; search-index, Kafka, and Catalog.Api keep the signals they already have; a container with no extra signals still shows up).
3. **Given** the store is up but the catalog has no products yet, **When** the operator reads store size on store detail, **Then** they do not treat a small or zero size as not-healthy or as a cluster-down condition.
4. **Given** the metrics path is stopped, **When** an operator creates a product, **Then** the write and get-by-id still succeed.

---

### User Story 2 - Catalog application logs appear once (Priority: P1)

A developer looks up a Catalog request in Grafana. They see that request’s application diagnostic lines **once**. They do not see the same Catalog request line twice — once from application export and once again as a container copy of the same console output.

**Why this priority**: The operator already has application logging in Grafana. Shipping Catalog container stdout on top of that is the duplicate they asked to avoid. Coverage of other containers (US1) must not double Catalog request logs.

**Independent Test**: Generate at least one Catalog request with the log viewer up. Search for Catalog application diagnostics in a recent window. The same request line is not listed twice from two collection paths. Isolated tests do not start the viewer.

**Acceptance Scenarios**:

1. **Given** Catalog has handled at least one public request and application export is on, **When** the operator searches the log viewer for that Catalog request, **Then** those application lines appear from the application path and are not also listed as a second container-tail copy of the same text.
2. **Given** Catalog and another container (for example the Kafka broker) both have recent lines, **When** the operator filters to Catalog, **Then** they do not need the other container’s process lines to complete the Catalog story.
3. **Given** a Catalog request failed and recorded an error, **When** the operator filters to Catalog errors, **Then** they can still read request name, duration, and stack when those were recorded (same usefulness as today).

---

### User Story 3 - Keep product work working when collection is down (Priority: P2)

An operator starts Catalog without the container-log collection path or Grafana. Create, import, get-by-id, and isolated automated checks still succeed. Collection stays optional for daily product work.

**Why this priority**: Catalog already starts without Grafana and collectors. Expanding which containers are shipped must not become a startup gate.

**Independent Test**: Leave the collector and Grafana stopped. Confirm Catalog starts, create and get-by-id succeed, and isolated test suites pass.

**Acceptance Scenarios**:

1. **Given** the container-log collection path is stopped, **When** an operator creates a product, **Then** the write succeeds and get-by-id still returns the stored product.
2. **Given** Grafana is stopped, **When** Catalog handles a request, **Then** the process stays up and local application diagnostics still appear.
3. **Given** isolated automated checks run, **When** those suites execute, **Then** they complete without starting the collector, Grafana, or other stack containers.

---

### Edge Cases

- Collection is up before a container: searches succeed; results stay empty until that container writes something.
- A container is up before collection: that process keeps working; once collection starts, **new** process lines appear (backfill of old files is not required).
- Collection or Grafana is down: Catalog and Kafka keep running; operators use local console or the topic UI until the stack is back.
- Catalog application export (existing path) stays the Catalog request-log path. This feature must not add a second copy of those same Catalog application lines via container collection.
- Hybrid Ops/Infra **becomes the one stack-health overview** of the product stack (store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, metric exporters). Click a container for its detail metrics, including store up, connections, and size. **Healthy** means the process is up. Stopped or missing is not healthy. Empty catalog data is not an outage. The log collector and Grafana are not required on this board. The existing log view stays the process-line search. The Elasticsearch application-error view stays. This feature does not require operators to use Elasticsearch for container process logs.
- Metrics path down: Catalog writes and get-by-id still work; operators use the store process log or the database console until the overview and detail metrics return. A missing or stopped container on the overview is “not healthy,” not a Grafana configuration error.
- Store health MUST NOT include SQL statement text or per-query payloads.
- Isolated and service-level Catalog checks do not start the collector or Grafana.
- Default local Grafana sign-in is unchanged.
- Slack/email paging, production TLS, and shipping Kafka **topic payloads** as the log path are out of scope.
- Collecting the log viewer’s own process output, or the collector’s own output, is optional; a loop of collector-collects-itself is not required. Those two also need not appear on the Ops/Infra health overview.
- Future application services that export their own diagnostics the same way as Catalog MUST follow the same rule: one application stream, not application plus a container copy of the same lines.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Operators MUST be able to find process or container output for every local-stack container that does **not** already export application diagnostics, using the existing Grafana log view. They MUST NOT add a log data source by hand.
- **FR-002**: After a target container is healthy and has written process output, an operator MUST be able to filter the last 15 minutes by that container’s identity and see those lines, separately from other containers.
- **FR-003**: Catalog application diagnostic lines MUST continue to reach Grafana through the existing application export path. The container collection path MUST NOT also ship a second copy of those same Catalog application lines.
- **FR-004**: The collection path MUST ship process or container output. It MUST NOT ship Kafka topic message contents as the log path (the topic UI remains the topic viewer). Catalog MUST NOT become the scraper of the log store and MUST NOT gain a new public HTTP route for this feature.
- **FR-005**: Catalog process start MUST NOT require the collector, Grafana, or the log store. Create, import, update, and get-by-id MUST keep working when those are down.
- **FR-006**: Isolated and service-level automated checks MUST complete without starting the collector, Grafana, or the log store.
- **FR-007**: This feature MUST reuse the existing local compose pair and the existing Grafana. It MUST NOT add a second compose file or a second metrics/log viewer.
- **FR-008**: This feature MUST NOT remove the existing log view, Ops/Infra (now the stack-health overview with click-through detail), the Elasticsearch application-error view, document inspection, or the topic UI.
- **FR-009**: Run notes MUST tell an operator how to start collection with Grafana, how to query container process logs by identity, that Catalog application logs are the application path (not a second container copy), and how to use the Ops/Infra overview (healthy or not) then open a container’s detail metrics.
- **FR-010**: Local-dev collection access MUST use the existing no-auth local defaults. Production auth and TLS are out of scope.
- **FR-011**: A later service that exports its own application diagnostics MUST be treatable the same way as Catalog: operators see those application lines once.
- **FR-012**: Ops/Infra MUST be the single stack-health overview of the **product stack**: store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, and metric exporters. Each MUST show healthy or not. **Healthy** MUST mean that container’s process is up. Stopped or missing MUST show as not healthy. The log collector and Grafana MUST NOT be required on this overview. Ops/Infra MAY show LGTM container disk and RAM as optional storage detail (Grafana SQLite plus Loki/Prometheus/Tempo in that container). That row MUST NOT be a required healthy/not identity. Operators MUST NOT add a metrics data source by hand. Clicking a container MUST open that container’s detail metrics in the same Grafana. **Every product-stack identity’s detail MUST include CPU and RAM.** Catalog-store detail MUST also include up, connection count, and size. Small or zero size MUST NOT be treated as not-healthy or as a store outage. Search-index, Kafka, and Catalog.Api MUST keep the richer signals they already have as that container’s detail.
- **FR-013**: Store health MUST come from a metrics scrape of the store (same hybrid pattern as search-index and Kafka). Catalog MUST NOT scrape the store for those cluster signals. Catalog MUST NOT wait on that scrape path to start.
- **FR-014**: Store health MUST NOT expose SQL statement text or per-query payloads.

### Key Entities

- **Container process line**: One process or container stdout/stderr event (startup, listeners, errors) from a stack container that does not already export application diagnostics.
- **Application diagnostic line**: One Catalog (or later service) application log event already sent to Grafana by the application export path. Must not be duplicated by container collection.
- **Log collection path**: The existing local shipper that forwards process output into the existing Grafana log store. Expanded coverage, not a second viewer.
- **Existing Grafana**: The current local metrics and log viewer. One instance. No second UI.
- **Container health status**: One overview reading per **product-stack** container on Ops/Infra (store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, metric exporters): healthy or not. Healthy = process is up. Stopped or missing = not healthy. Empty data is not not-healthy. Log collector and Grafana are not required here.
- **Container detail metrics**: The metrics shown after the operator clicks a container on that overview (same Grafana). Store detail includes up, connections, size — not SQL text. Search-index, Kafka, and Catalog.Api keep existing richer signals. Other containers may show only up.
- **Store health sample**: The catalog store’s detail metrics: up, connections, size. Not SQL text.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After the documented local stack is up, an operator can find process lines for each non-application stack container (after that container is healthy) in the existing log viewer in under 2 minutes, with no manual data-source setup, and can tell those containers apart.
- **SC-002**: After one Catalog request, an operator searching Catalog application diagnostics in a recent window sees those lines once, not twice from two paths, in under 2 minutes.
- **SC-003**: Isolated Catalog and building-block test suites still complete without starting the collector or Grafana.
- **SC-004**: With the collector or Grafana stopped, a valid create still succeeds and get-by-id still returns the stored product.
- **SC-005**: An operator can tell “no logs yet” from “log viewer is misconfigured” using only an empty-result state versus a connection or configuration error.
- **SC-006**: After the catalog store has been healthy for one minute, an operator can tell whether the store is up and see recent connection and size signals on the store’s Ops/Infra detail in under 2 minutes, without treating a small empty catalog as an outage.
- **SC-007**: After the documented local stack is up, an operator can tell process-up (healthy) from stopped or missing (not healthy) for each product-stack container (store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, exporters) on the one Ops/Infra overview in under 2 minutes, then open one container’s detail metrics in the same Grafana, with no manual data-source setup. A small empty catalog must not be read as not-healthy. The log collector and Grafana need not appear on that board.

## Assumptions

- Target environment is the existing local compose stack and the existing Grafana already used for Catalog and Kafka broker logs. This is not a production hardening pass.
- **All containers** means every local-stack process that operators debug from stdout (store, broker, connector, search index, operator UIs, metric exporters, and similar). It does **not** mean a second copy of Catalog application request logs.
- Catalog (and later application services) keep the existing application export into Grafana. Container collection covers the rest. How that split is implemented is plan-time.
- Kafka **topic payloads** stay in the topic UI. Process logs stay in Grafana.
- Hybrid observability stays: Ops/Infra is the one stack-health overview of the product stack (store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, exporters). **Healthy** means the process is up; stopped or missing is not healthy; empty data is not an outage. The log collector and Grafana are not required on that board. Existing search-index, Kafka, and Catalog.Api signals remain that container’s detail; this slice also adds catalog-store detail (up, connections, size). SQL query logging is out of scope. The existing log view stays the process-line search.
- Isolated tests stay in-memory and do not start containers.
- No new public Catalog HTTP route is required.
- Slack/email paging and multi-cluster federation are out of scope.
- The current collector already ships Catalog and Kafka broker process lines; this feature expands coverage and **removes the Catalog container duplicate** of application lines.
