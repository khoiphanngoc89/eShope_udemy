# Feature Specification: Activate Loki in Grafana

**Feature Branch**: `002-activate-loki-grafana`

**Created**: 2026-10-25

**Status**: Draft

**Input**: User description: "I want to active the Loki in grafana"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Open Grafana and query logs through Loki immediately (Priority: P1)

An operator starts the local observability stack and opens Grafana. Loki is already present as the log source — they do not add a data source, paste a URL, or follow extra setup steps. They open the log explorer, pick Loki, choose a recent time range, and run a search. Grafana returns either matching logs or a clear empty-result state.

**Why this priority**: The request is to activate Loki in Grafana. Until Loki is ready as a usable log source at first open, nothing else in this feature delivers value.

**Independent Test**: Start only the local observability stack (no application services required). Open Grafana, confirm Loki is listed as an available log source, and run a search for the last 15 minutes. The explorer works without any manual data-source setup.

**Acceptance Scenarios**:

1. **Given** the local observability stack has started successfully, **When** an operator opens Grafana, **Then** Loki is listed as an available, enabled log source.
2. **Given** Loki is listed as a log source, **When** the operator opens the log explorer and selects Loki, **Then** they can set a time range and run a search without adding or editing a data source by hand.
3. **Given** a first-time or freshly started stack with no stored logs yet, **When** the operator runs a search against Loki, **Then** Grafana shows a clear “no matching logs” result rather than a configuration or connection error.

---

### User Story 2 - Find a service’s recent diagnostic records in Loki (Priority: P2)

A developer or support engineer has a service (Catalog first) that is already writing diagnostic records into the existing observability pipeline. They open Grafana, query Loki, and find those records. They can narrow results by service identity, severity, and time range so they see only the service and period they care about.

**Why this priority**: Activating Loki is only useful if the logs services already emit actually appear there. This story depends on Loki being queryable (P1) and on services already exporting diagnostics (existing logging feature).

**Independent Test**: With the observability stack running and Catalog (or another enabled service) writing diagnostics, open Grafana, query Loki for that service’s identity over the last 15 minutes, and confirm the same recent records are present and filterable.

**Acceptance Scenarios**:

1. **Given** a service is exporting diagnostic records through the existing observability pipeline and Loki is active in Grafana, **When** an operator searches Loki for that service’s identity in a recent time range, **Then** those records appear in the results.
2. **Given** more than one service has written diagnostics in the same period, **When** the operator filters Loki by one service’s identity, **Then** only that service’s records are shown.
3. **Given** records of mixed severities exist, **When** the operator filters by a chosen severity (for example errors only), **Then** results include only records of that severity.
4. **Given** a record was written during a traced request, **When** the operator inspects that record in Loki, **Then** they can see the same correlation identifiers that appear on the local diagnostic output so they can tie the log back to one request.

---

### User Story 3 - Understand when Loki is not ready or has nothing to show (Priority: P3)

An operator opens Grafana while Loki is still starting, has been stopped, or has no logs for the chosen filters. Grafana does not fail silently. The operator sees a clear message that the log source is unavailable, still starting, or that the query matched nothing — and they know whether to wait, restart the stack, or widen the search.

**Why this priority**: Local stacks start in pieces. A confusing blank screen would make “Loki is activated” look broken even when the product is working. This story is independent of live service traffic.

**Independent Test**: Open Grafana with Loki stopped or not yet ready and confirm a clear unavailable/starting message. Separately, with Loki healthy and a query that matches nothing, confirm an empty-result message rather than an error.

**Acceptance Scenarios**:

1. **Given** Grafana is reachable but Loki is stopped or not yet ready, **When** the operator opens the log explorer or runs a Loki search, **Then** Grafana reports that the log source is unavailable or still starting, not a generic failure.
2. **Given** Loki is healthy and the chosen time range or filters match no records, **When** the operator runs the search, **Then** they see an explicit empty result and can change the range or filters.
3. **Given** Loki later becomes healthy after a wait or stack restart, **When** the operator retries the same search, **Then** the explorer works without the operator adding a data source.

---

### Edge Cases

- The observability stack is started for the first time: Loki is already activated; the operator is not asked to create a data source.
- Grafana is up before Loki finishes starting: the operator sees a starting/unavailable state, then can retry.
- No service has exported diagnostics yet: Loki searches succeed and return empty results.
- The existing service export path is disabled or the collector is down: Grafana and Loki still start; searches return empty results rather than breaking Grafana.
- Two services write at the same time: records remain distinguishable by service identity.
- The operator chooses a time range that predates any stored logs: empty result, not an error.
- A large volume of recent logs is present: the explorer remains usable for a typical local session; the operator can narrow by time, service, or severity instead of being blocked.
- Default local Grafana sign-in is used: activating Loki does not introduce a new login flow.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: When the local observability stack starts successfully, Grafana MUST present Loki as an enabled log source with no manual data-source setup by the operator.
- **FR-002**: Operators MUST be able to open Grafana’s log explorer, select Loki, choose a time range, and run a search on first use.
- **FR-003**: Diagnostic records that services already export through the existing observability pipeline MUST be available to query through Loki in Grafana.
- **FR-004**: Operators MUST be able to filter Loki results by service identity, severity, and time range.
- **FR-005**: When a stored record includes request correlation identifiers, Grafana’s Loki result for that record MUST show those identifiers.
- **FR-006**: A search that matches no records MUST show a clear empty-result state, not a configuration or connection error.
- **FR-007**: When Loki is stopped or not yet ready, Grafana MUST tell the operator that the log source is unavailable or still starting.
- **FR-008**: Activating Loki MUST NOT require operators to change how services write or export diagnostic records.
- **FR-009**: Activating Loki MUST NOT replace or disable Grafana itself; Grafana remains the place operators open to search logs.
- **FR-010**: A first-time start of the local observability stack MUST leave Loki activated for the next Grafana session without repeating setup.

### Key Entities

- **Loki**: The designated log store that Grafana queries after this feature is activated.
- **Grafana workspace**: The operator’s starting place for exploring logs; Loki appears there as a ready log source.
- **Log record**: A stored diagnostic event with timestamp, severity, message, service identity, and optional correlation identifiers.
- **Log query**: A search against Loki defined by time range and filters (service identity, severity, and free-text message when provided).
- **Service identity**: The label that distinguishes one service’s records from another in Loki results.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After a successful start of the local observability stack, an operator can open the viewer and search logs within 2 minutes, without adding a log source by hand.
- **SC-002**: After a service writes diagnostic records into the existing pipeline, an operator can find those records in the viewer within 2 minutes.
- **SC-003**: An operator can isolate one service’s logs from others in under 30 seconds using service-identity filtering.
- **SC-004**: 100% of first-time successful stack starts present the log source as already enabled; no extra setup step is required on the second visit.
- **SC-005**: When a search matches nothing, 100% of those attempts show an explicit empty result rather than an error.
- **SC-006**: At least 90% of operators following the documented local steps can complete “find this service’s logs from the last 15 minutes” on the first attempt.

## Assumptions

- Product constraint from the request: the log store operators query from Grafana is Loki. Grafana remains the viewer. These names are constraints, not implementation steps.
- This feature activates Loki on the existing local Grafana observability stack already used for service diagnostics. It does not introduce a second viewer or a cloud-hosted Grafana.
- Services already export diagnostic records through the existing shared-logging export path. This feature does not change service logging configuration, destinations, or Catalog startup.
- Grafana Cloud, a separate production Loki cluster, and sending service logs straight to Loki (bypassing the existing collector path) are out of scope.
- Alerting rules, on-call notifications, and a full custom dashboard catalog are out of scope. A working log explorer against Loki is the success path.
- Default local Grafana sign-in already documented for the stack (administrator account on the local UI) is reused. This feature does not add a new identity provider.
- Local retention follows the stack’s default short-lived developer retention. Long-term compliance retention is out of scope.
- Catalog is the first producing service used to prove records appear in Loki. Later services that use the same export path appear the same way.
- “Activate” means Loki is ready, enabled, and selectable in Grafana when the stack is up — not merely that a Loki process exists unused.
- Metrics and traces may already be present in the same local stack; this feature does not require enabling or changing them.
