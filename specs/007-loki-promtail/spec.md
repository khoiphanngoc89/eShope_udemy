# Feature Specification: Modern Grafana Log Collection

**Feature Branch**: `007-loki-promtail`

**Created**: 2026-11-01

**Status**: Draft

**Review notes**: [how it works + how to implement](./review-notes.md)

**Input**: User description: "Option 1: The Modern Grafana Way (Loki + Promtail/Grafana Agent)"

## Clarifications

### Session 2026-11-01

- Q: When you say you want to log Kafka in Grafana, which Kafka output should the collector send to Loki? → A: Catalog and Kafka broker process logs in Loki (not topic payloads; not Connect or Kafka UI logs in this slice)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See Catalog logs in the existing Grafana log viewer via a collector (Priority: P1)

An operator starts the local stack, including a Grafana-family log collector (Promtail or Grafana Agent). They open the existing Grafana, pick Loki (already there — they do not add a data source), and see Catalog’s recent diagnostic lines. Those lines arrive because the collector shipped container or process output to Loki, not because the operator pasted a log file or opened a second log product.

**Why this priority**: Option 1 is only real when Loki shows Catalog logs collected the Grafana way. Until that path works, nothing else in this feature delivers value.

**Independent Test**: Start the documented local stack with the collector and Grafana. Generate at least one Catalog request. In Grafana Explore → Loki, search the last 15 minutes for Catalog. Matching lines appear without adding a data source.

**Acceptance Scenarios**:

1. **Given** Grafana and the log collector have started successfully, **When** an operator opens Grafana, **Then** Loki is already listed and they can search without adding a data source by hand.
2. **Given** Catalog has written recent diagnostic output, **When** the operator searches Loki for Catalog in the last 15 minutes, **Then** those lines appear.
3. **Given** a first start with no Catalog traffic yet, **When** the operator runs the same search, **Then** they see a clear empty result, not a configuration or connection error.

---

### User Story 1b - See Kafka broker process logs in Grafana (Priority: P1)

An operator starts Kafka, the log collector, and Grafana. They open the existing Grafana, pick Loki, and filter to the Kafka broker. They see the broker’s recent **process** diagnostic lines (startup, listeners, errors). They do not see Kafka **topic message contents** (those stay in Kafka UI). They do not add a data source.

**Why this priority**: The operator asked to log Kafka in Grafana. Until broker process logs appear in Loki next to Catalog, Option 1 is incomplete for the messaging stack.

**Independent Test**: Start Kafka, the collector, and Grafana (Catalog not required). After the broker is healthy, search Loki for the Kafka broker in the last 15 minutes. Process lines appear; a search does not return topic payloads as the primary result.

**Acceptance Scenarios**:

1. **Given** Kafka, the collector, and Grafana have started successfully, **When** an operator searches Loki for the Kafka broker in the last 15 minutes, **Then** broker process lines appear without adding a data source.
2. **Given** Catalog and Kafka both have recent lines, **When** the operator filters Loki to the Kafka broker, **Then** Catalog lines are not shown.
3. **Given** products are flowing on CDC topics, **When** the operator uses this Loki path, **Then** they are looking at broker process logs, not the message bodies (Kafka UI remains the place for topic contents).

---

### User Story 2 - Find why a request failed in Loki (Priority: P2)

A developer already has a failed Catalog request (validation error, missing product, or search failure). They stay in Grafana, filter Loki to Catalog and to errors (or the last few minutes), and read the failed-request text — request name, duration, and stack when those were recorded. They do not switch to Elasticsearch Log/App or Kibana to complete this story.

**Why this priority**: The Modern Grafana Way is useful when “why it broke” is answerable in Loki. P1 only proves lines exist; this story proves an operator can isolate the failure.

**Independent Test**: Cause one Catalog failure that records an error. Query Loki for Catalog errors in a recent window and confirm that error text is present and filterable.

**Acceptance Scenarios**:

1. **Given** a Catalog request has failed and an error was recorded, **When** the operator filters Loki to Catalog and to error severity in a recent time range, **Then** they see that error text.
2. **Given** successful and failed requests exist in the same window, **When** the operator filters to errors, **Then** successful Information-only lines are not required to understand the failure.
3. **Given** the failed request had a correlation id, **When** the operator inspects the Loki line, **Then** they can see the same trace or span identifiers that appear on the local console output when those were attached.

---

### User Story 3 - Keep product work working when the collector is down (Priority: P3)

An operator starts Catalog without the log collector or Grafana. Create, import, get-by-id, and isolated automated checks still succeed. The collector is optional for daily product work.

**Why this priority**: Catalog already starts without Grafana, the search-index exporter, and Kibana. A log collector must not become a startup gate.

**Independent Test**: Leave the collector and Grafana stopped. Confirm Catalog starts, create and get-by-id succeed, and isolated test suites pass.

**Acceptance Scenarios**:

1. **Given** the log collector is stopped, **When** an operator creates a product, **Then** the write succeeds and get-by-id still returns the stored product.
2. **Given** Grafana or Loki is stopped, **When** Catalog handles a request, **Then** the process stays up and console diagnostics still appear.
3. **Given** isolated automated checks run, **When** those suites execute, **Then** they complete without starting the collector, Grafana, or Loki.

---

### Edge Cases

- Collector is up before Catalog: Loki searches succeed; results stay empty until Catalog writes something.
- Catalog is up before the collector: product work succeeds; once the collector starts, new lines appear in Loki (backfill of old files is not required).
- Collector or Loki is down: Catalog does not fail start or writes; operators use the console until the stack is back.
- Existing Serilog → OTLP → Loki path (002) may still deliver lines. Operators may see overlap; this feature does not require removing that path.
- Hybrid Ops/Infra and Elasticsearch Log/App (006) stay available. This feature does not replace cluster-health metrics or require operators to use Elasticsearch for this story.
- Isolated and service-level Catalog checks do not start the collector or Grafana.
- Default local Grafana sign-in is unchanged.
- Slack/email paging, production TLS, and collecting Connect, Kafka UI, databases, or Kafka **topic payloads** are out of scope for this slice.
- Kafka broker is up before the collector: broker work continues; once the collector starts, new process lines appear in Loki.
- Collector is down: Kafka keeps running; operators use container logs or Kafka UI until the collector is back.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Operators MUST get Catalog and Kafka **broker process** diagnostic lines into the existing Grafana Loki using a Grafana-family log collector (Promtail or Grafana Agent). They MUST NOT add a Loki data source by hand.
- **FR-002**: After Catalog has handled at least one public request, an operator MUST be able to find those lines in Grafana Explore → Loki by service identity in a recent time range. After the Kafka broker is healthy, they MUST be able to find broker process lines the same way and filter them separately from Catalog.
- **FR-003**: After a Catalog request fails and records an error, an operator MUST be able to filter Loki to Catalog errors and read the failed-request text (request name, duration, and stack when recorded).
- **FR-004**: The collector MUST ship Catalog and Kafka broker process or container output. It MUST NOT ship Kafka topic message contents as the log path (Kafka UI remains the topic viewer). Catalog MUST NOT become the scraper of Loki and MUST NOT gain a new public HTTP route for this feature.
- **FR-005**: Catalog process start MUST NOT require the collector, Grafana, or Loki. Create, import, update, and get-by-id MUST keep working when those are down.
- **FR-006**: Isolated and service-level automated checks MUST complete without starting the collector, Grafana, or Loki.
- **FR-007**: This feature MUST reuse the existing local compose pair and the existing Grafana. It MUST NOT add a second compose file or a second metrics/log viewer.
- **FR-008**: This feature MUST NOT remove Loki Explore, Ops/Infra (Prometheus), Elasticsearch Log/App, Kibana document inspection, or Kafka UI.
- **FR-009**: Run notes MUST tell an operator how to start the collector with Grafana and how to query Catalog versus the Kafka broker in Loki (Grafana-native process logs, not Elasticsearch Log/App, not topic payloads).
- **FR-010**: Local-dev collector-to-Loki access MUST use the existing no-auth local defaults. Production auth and TLS are out of scope.

### Key Entities

- **Log collector**: A Grafana-family shipper (Promtail or Grafana Agent) that reads Catalog and Kafka broker process output and forwards it to Loki.
- **Loki log view**: The existing Grafana Explore → Loki path. Answers “what did Catalog or the Kafka broker log?” and “why did this Catalog request fail?”
- **Catalog diagnostic line**: One log event with time, message, severity, service identity, and optional trace/span identifiers.
- **Kafka broker diagnostic line**: One Kafka broker process log event (startup, listeners, errors). Not a topic message body.
- **Existing Grafana**: The current local metrics and log viewer. One instance. No second UI.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After the documented local stack is up, an operator can find Catalog lines (after one request) and Kafka broker process lines (after the broker is healthy) in the existing log viewer in under 2 minutes each, with no manual data-source setup, and can tell the two services apart.
- **SC-002**: After one failed Catalog request that records an error, an operator can filter to Catalog errors and see that error text in under 2 minutes.
- **SC-003**: Isolated Catalog and building-block test suites still complete without starting the collector or Grafana.
- **SC-004**: With the collector or Grafana stopped, a valid create still succeeds and get-by-id still returns the stored product.
- **SC-005**: An operator can tell “no logs yet” from “log viewer is misconfigured” using only an empty-result state versus a connection or configuration error.

## Assumptions

- Target environment is the existing local compose stack (Catalog, search index, LGTM Grafana with Loki already provisioned). This is not a production hardening pass.
- Requested architecture is **Option 1 — The Modern Grafana Way**: a Promtail or Grafana Agent collector ships logs to Loki; Grafana remains the one viewer. Choosing Promtail vs Grafana Agent (or Alloy) is a plan-time detail; operators see one collector service.
- v1 collects **Catalog** and **Kafka broker process** diagnostics only. Debezium Connect, Kafka UI, databases, and Kafka **topic payloads** are out of this slice (Kafka UI stays the topic viewer).
- Existing Serilog console + OTLP export (001/002) stays. The collector is an additional Grafana-native path so logs still reach Loki from container/process output. Overlap is acceptable; removing OTLP is out of scope unless a later spec says so.
- Hybrid observability (006) stays: Prometheus Ops/Infra and Elasticsearch Log/App are not removed. This feature does not change cluster-health alerts.
- Isolated tests stay in-memory and do not start containers.
- No new public Catalog HTTP route is required.
- Slack/email paging and multi-cluster federation are out of scope.
