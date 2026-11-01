# Feature Specification: Hybrid Ops and App Observability

**Feature Branch**: `006-hybrid-observability`

**Created**: 2026-11-01

**Status**: Draft

**Review notes**: [how it works + how to implement](./review-notes.md)

**Input**: User description: "I want to use The Hybrid Architecture • Prometheus Data Source → Powers your Ops/Infra Dashboards (Tells you if the system is broken via CPU, Heap, and Shard alerts). • Elasticsearch Data Source → Powers your Log/App Dashboards (Tells you why it broke by showing the exact error logs or application exceptions)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See if the system is broken (Ops/Infra) (Priority: P1)

An operator opens the existing local metrics viewer. An Ops/Infra dashboard, powered by the Prometheus data source, answers one question: is the search-index cluster broken? They see CPU, heap, and shard signals, and local alerts fire when those signals say the cluster is under pressure or down. They do not add a data source by hand. An empty catalog (no products indexed yet) is not treated as a broken cluster.

**Why this priority**: Catalog browse already depends on the search index. Until an operator can tell “cluster is down / heap is exhausted / shards are unhealthy” from one infra view, they cannot start diagnosis.

**Independent Test**: Start the documented local observability stack and the search index. Open the Ops/Infra view. Confirm CPU, heap, and shard signals are visible, and that a healthy empty index does not raise a cluster-down alert.

**Acceptance Scenarios**:

1. **Given** the local observability stack and search index have started successfully, **When** an operator opens the existing metrics viewer, **Then** a Prometheus data source is already present and they can open the Ops/Infra dashboard without adding a data source by hand.
2. **Given** the search index is healthy, **When** the operator reads Ops/Infra, **Then** they see live CPU, heap, and shard signals and a healthy cluster status.
3. **Given** the search index is up but no products have been indexed yet, **When** the operator reads Ops/Infra, **Then** they see a live cluster, not a broken-cluster alert caused only by zero documents.
4. **Given** CPU, heap, or shard signals cross the documented local thresholds, **When** the operator looks at Ops/Infra alerts, **Then** they see a clear “system is broken / under pressure” signal.

---

### User Story 2 - See why it broke (Log/App) (Priority: P2)

A developer or support engineer already suspects a break from Ops/Infra, or they have a failed Catalog request. They open a Log/App dashboard in the same metrics viewer, powered by an Elasticsearch data source. They see the exact error log or application exception that explains why it broke. They do not add that data source by hand and they do not switch to a second product for that diagnosis.

**Why this priority**: Infra alerts only say the system is unhealthy. The hybrid split is complete only when the same viewer shows the matching error text.

**Independent Test**: Cause one failed Catalog request that records an error or exception. Open the Log/App view and confirm that error text appears in a recent time range.

**Acceptance Scenarios**:

1. **Given** the metrics viewer and search index are up, **When** an operator opens the Log/App dashboard, **Then** an Elasticsearch data source is already present and they do not add it by hand.
2. **Given** a Catalog request has failed and an error or exception was recorded, **When** the operator queries Log/App for a recent time range, **Then** they see that exact error or exception text.
3. **Given** Ops/Infra shows cluster pressure or a firing alert, **When** the operator switches to Log/App, **Then** they can look for matching application errors in the same recent window.
4. **Given** more than one service has written diagnostics, **When** the operator filters Log/App to Catalog, **Then** they see Catalog errors only.

---

### User Story 3 - Keep product work working when dashboards are down (Priority: P3)

An operator starts Catalog without Prometheus, the Elasticsearch log view, or the metrics viewer. Create, import, get-by-id, and isolated automated checks still succeed. Hybrid dashboards are optional for daily product work.

**Why this priority**: Catalog already starts without Kibana, Kafka UI, or the log viewer. Hybrid observability must not become a startup gate.

**Independent Test**: Leave the metrics viewer and cluster-metrics collector stopped. Confirm Catalog starts, create and get-by-id succeed, and isolated test suites pass.

**Acceptance Scenarios**:

1. **Given** Prometheus or the Ops/Infra collector is stopped, **When** an operator creates or imports a product, **Then** the write succeeds and get-by-id still returns the stored product.
2. **Given** the Elasticsearch Log/App view is unavailable and the search index is still up, **When** a client asks for a product page, **Then** browse behaves as it does today — it does not fail only because dashboards are missing.
3. **Given** isolated automated checks run, **When** those suites execute, **Then** they complete without starting the search index or the metrics viewer.

---

### Edge Cases

- First start with a live, empty search index: Ops/Infra shows a healthy cluster; Log/App may be empty. Neither is a configuration error.
- Search index is down: Ops/Infra shows unreachable or firing alerts; Catalog writes and get-by-id still work.
- Metrics viewer is up before collectors finish starting: operator sees no samples or a starting state, then retries.
- Log/App has no error documents yet: clear empty result, not a data-source error.
- Isolated and service-level Catalog checks do not start the metrics viewer or the search index.
- Existing Loki explore and Kibana document inspection stay available; they are not the hybrid Log/App path.
- Default local metrics-viewer sign-in is unchanged.
- Slack/email paging and production credentials/TLS are out of scope. Local alert rules for CPU, heap, and shards are in scope.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Operators MUST use a hybrid split in the existing local metrics viewer: Prometheus powers Ops/Infra dashboards (“is it broken?”); Elasticsearch powers Log/App dashboards (“why did it break?”). Both data sources MUST be present without manual setup.
- **FR-002**: Ops/Infra MUST show search-index CPU, heap, and shard signals, plus cluster health, so an operator can tell if the system is broken.
- **FR-003**: Ops/Infra MUST include local alert rules for CPU, heap, and shards. A healthy empty index MUST NOT fire a cluster-down alert.
- **FR-004**: Cluster signals for Ops/Infra MUST come from Prometheus scraping a search-index exporter. Catalog MUST NOT scrape the search index itself for those cluster signals.
- **FR-005**: Log/App MUST be powered by an Elasticsearch data source and MUST show the exact error log or application exception for a recent failed Catalog request.
- **FR-006**: Catalog MUST record failed-request errors and exceptions so the Log/App view can show them. Existing Loki export MAY continue; it is not the hybrid Log/App path.
- **FR-007**: Catalog process start MUST NOT require Prometheus, the exporter, the Elasticsearch log view, or the metrics viewer. Create, import, update, and get-by-id MUST keep working when those are down.
- **FR-008**: Isolated and service-level automated checks MUST complete without the search index or the metrics viewer.
- **FR-009**: This feature MUST reuse the existing local compose pair. It MUST NOT add a second compose file or a second metrics viewer.
- **FR-010**: This feature MUST NOT replace Kibana document inspection, Kafka UI, or Catalog application search meters (page/search latency, search errors, sync lag).
- **FR-011**: Run notes MUST tell an operator how to start the hybrid stack and how to open Ops/Infra versus Log/App.
- **FR-012**: Local-dev access to the search index for the exporter and the Elasticsearch data source MUST use the existing no-auth local defaults. Production auth and TLS are out of scope.

### Key Entities

- **Ops/Infra dashboard**: Prometheus-backed view that answers “is the system broken?” using CPU, heap, shard, and cluster-health signals and alerts.
- **Log/App dashboard**: Elasticsearch-backed view that answers “why did it break?” using exact error logs or application exceptions.
- **Prometheus data source**: Already in the local metrics viewer; used only for Ops/Infra in this feature.
- **Elasticsearch data source**: Added in the same metrics viewer; used only for Log/App in this feature.
- **Search-index cluster**: The local search index that serves catalog browse and is the target of cluster CPU/heap/shard signals.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After the documented local stack is up, an operator can open Ops/Infra and tell whether the search-index cluster is healthy in under 2 minutes, with no manual data-source setup.
- **SC-002**: After the search index has been healthy for one minute, Ops/Infra shows recent CPU, heap, and shard signals.
- **SC-003**: After one failed Catalog request that records an error, an operator can open Log/App and see that error text in under 2 minutes.
- **SC-004**: Isolated Catalog and building-block test suites still complete without starting the metrics viewer or the search index.
- **SC-005**: With Ops/Infra or Log/App stopped, a valid create or import still succeeds and get-by-id still returns the stored product.
- **SC-006**: An operator can tell a healthy empty cluster from a broken cluster using only Ops/Infra (zero documents vs firing CPU/heap/shard or unreachable status).

## Assumptions

- Target environment is the existing local compose stack (Catalog, search index, Kafka/Debezium sync, LGTM metrics viewer). This is not a production hardening pass.
- Requested architecture is **hybrid**: Prometheus → Ops/Infra (is it broken); Elasticsearch → Log/App (why it broke).
- Ops/Infra cluster metrics use a Prometheus scrape of a search-index exporter (Method 1). Catalog does not scrape the cluster.
- Catalog failed-request errors and exceptions are also written so the Elasticsearch Log/App view can show them. Existing Loki explore is kept; it is not removed and is not the hybrid Log/App path.
- Isolated tests stay in-memory and do not start containers.
- No new public Catalog HTTP route is required.
- Slack/email paging and multi-cluster federation are out of scope.
