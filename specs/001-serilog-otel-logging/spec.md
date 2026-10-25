# Feature Specification: Shared Structured Logging with Trace Correlation

**Feature Branch**: `001-serilog-otel-logging`

**Created**: 2026-10-25

**Status**: Draft

**Input**: User description: "I want t use Serilog. I want to implment config in BuildingBlock.Logging. It aslo to integrate with opentelemetry. the config should in appsettings in each service"

## Clarifications

### Session 2026-10-25

- Q: Where should each service send its diagnostic records so they appear in Grafana? → A: Local Grafana Alloy/collector OTLP URL in each service's application settings (typically `http://localhost:4318` over HTTP). Grafana reads logs from that pipeline. Grafana Cloud and direct Loki ingest are out of scope.
- Q: In which settings files should that Grafana OpenTelemetry endpoint be written so every log is exported? → A: The base application settings file includes `http://localhost:4318`. Other environments may override the URL. An empty URL means do not export.
- Q: How is the local Grafana collector hosted? → A: Operator hosts Grafana in Podman using the default OTLP HTTP endpoint `http://localhost:4318` (and UI on port 3000). This repo does not run Grafana inside the Catalog process.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Enable shared structured logging from one building block (Priority: P1)

A service team needs every running service to emit consistent, structured diagnostic records (timestamp, severity, message, and useful context) without each service inventing its own logging setup. They turn on the shared logging building block once at service startup. From that moment, application and platform messages use the shared logging behavior.

**Why this priority**: Without a single shared setup, later configuration and trace-correlation work has nothing to attach to. This is the minimum viable capability.

**Independent Test**: Start a service that uses the shared logging building block with a valid local configuration and confirm structured diagnostic records appear for startup and a sample request, with no service-specific logging code beyond enabling the shared setup.

**Acceptance Scenarios**:

1. **Given** a service that references the shared logging building block and has a valid logging configuration, **When** the service starts, **Then** it writes structured startup diagnostics that include timestamp, severity, message, and the service's identity.
2. **Given** a running service using the shared logging building block, **When** a request or business action is handled, **Then** diagnostic records for that action use the same structured shape as startup records.
3. **Given** a new service that has not written its own logging setup, **When** it enables the shared logging building block, **Then** it receives the same logging behavior as existing services without copying setup logic.

---

### User Story 2 - Control logging per service through that service's settings (Priority: P2)

An operator or developer needs different verbosity and destinations per service and environment (for example, quieter production Catalog logs, more detailed local Catalog logs) without changing the shared building block. They edit the service's own application settings file and restart the service.

**Why this priority**: Shared defaults are useful only if each service can override them. Per-service settings are the main operational control the request asked for.

**Independent Test**: Change one service's settings file (minimum severity and, if used, remote export endpoint), restart only that service, and confirm its diagnostic output changes while any other service is unaffected.

**Acceptance Scenarios**:

1. **Given** a service with logging settings in its application settings file, **When** the service starts, **Then** it applies those settings (minimum severity, outputs, service identity, and Grafana collector endpoint) without requiring a code change in the shared building block.
2. **Given** a service with a base settings file and an environment-specific settings file, **When** it starts in that environment, **Then** environment-specific logging values override the base values.
3. **Given** two services with different minimum severity settings, **When** both are running, **Then** each emits diagnostics according to its own settings only.
4. **Given** a service whose logging settings are missing required values, **When** the service starts, **Then** it either applies documented safe defaults or fails startup with a clear configuration error that names the missing setting.

---

### User Story 3 - Correlate diagnostic records with distributed traces (Priority: P3)

A support engineer investigating a failed customer request needs to jump between the request's trace and the related diagnostic records. When a request is being traced, every diagnostic record written during that request carries the same correlation identifiers so the engineer can filter to one request across services.

**Why this priority**: Trace correlation is the reason to connect logging with the observability pipeline. It is valuable once structured, configurable logging exists, but the platform still works locally without a remote collector.

**Independent Test**: Handle a request that has trace context, then confirm diagnostic records for that request include the same correlation identifiers as the trace. With the Grafana collector endpoint set, confirm records are exported; with the collector down or the endpoint empty, confirm the service still starts and writes local diagnostics.

**Acceptance Scenarios**:

1. **Given** a request that has active trace context, **When** the service writes diagnostics during that request, **Then** each record includes the same correlation identifiers as the trace so they can be filtered together.
2. **Given** a service with a Grafana collector OpenTelemetry endpoint in its settings, **When** it writes diagnostics, **Then** every record that meets the minimum severity is exported to that endpoint in addition to any local output, so the same records can be viewed in Grafana.
3. **Given** a service with no remote observability endpoint configured, or with the remote endpoint unreachable, **When** the service starts and handles requests, **Then** it still starts and continues writing local structured diagnostics.
4. **Given** a diagnostic record written outside any request, **When** it is emitted, **Then** it remains valid structured output and simply omits request-specific correlation identifiers.

---

### Edge Cases

- A service starts with an empty or missing logging section: documented safe defaults apply (local output at a standard operational severity), or startup fails with a named configuration error.
- Environment-specific settings conflict with base settings: environment-specific values win.
- Remote observability is misconfigured (invalid endpoint or credentials): the service does not crash request handling; the failure is visible in local diagnostics.
- Remote observability is slow or temporarily down: local diagnostic output continues; request handling is not blocked by export delays.
- High-volume diagnostic output: the service remains usable for normal catalog-style traffic; excess volume is controlled by per-service severity settings rather than by dropping the shared setup.
- Sensitive values (secrets, tokens, connection strings) appear in messages or settings: they must not be written to diagnostic output.
- Multiple services run on the same machine: records remain distinguishable by service identity and environment.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The platform MUST provide a single shared logging building block that any service can enable at startup to get consistent structured diagnostics.
- **FR-002**: The shared building block MUST own the logging setup and default behavior so services do not duplicate logging wiring.
- **FR-003**: Each service MUST supply its logging configuration in that service's own application settings files (base and environment-specific).
- **FR-004**: Logging configuration MUST support at least: minimum severity, one or more outputs, service identity, and a remote OpenTelemetry endpoint used to send records to the Grafana collector.
- **FR-005**: Changing a service's logging settings MUST take effect on the next restart of that service without a change to the shared building block.
- **FR-006**: Diagnostic records MUST be structured and MUST include timestamp, severity, message, service identity, and environment.
- **FR-007**: When a request has active trace context, diagnostic records written during that request MUST include the matching correlation identifiers.
- **FR-008**: When a Grafana collector OpenTelemetry endpoint is configured, the service MUST export every diagnostic record that meets the minimum severity to that endpoint as well as any local output (no extra sampling).
- **FR-009**: When remote observability is absent, invalid, or unreachable, the service MUST still start (unless local configuration itself is invalid) and MUST continue writing local diagnostics.
- **FR-010**: The shared building block MUST apply documented safe defaults when optional settings are omitted (local output, operational minimum severity, no remote export).
- **FR-011**: Diagnostic output MUST NOT include secrets, access tokens, or connection strings.
- **FR-012**: Catalog is the first consuming service; the same enable-and-configure pattern MUST work for later services without rewriting the building block.
- **FR-013**: Each service's **base** application settings file MUST include an OpenTelemetry export endpoint aimed at the local Grafana Alloy/collector (`http://localhost:4318` over HTTP) so every eligible record can be viewed in Grafana. Environment-specific settings MAY override that URL. An empty endpoint MUST disable export.

### Key Entities

- **Logging Configuration**: Per-service settings that control minimum severity, outputs, service identity, environment overrides, and the Grafana collector OpenTelemetry endpoint. The base settings file ships `http://localhost:4318`; environment files may replace that URL or clear it to disable export.
- **Diagnostic Record**: A single structured event with timestamp, severity, message, service identity, environment, and optional correlation identifiers.
- **Service Identity**: The name (and related labels) that distinguish one service's records from another's.
- **Trace Correlation**: Identifiers that tie diagnostic records to a distributed request so operators can filter one customer action across services.
- **Observability Export**: The OpenTelemetry path that forwards every eligible diagnostic record to the Grafana Alloy/collector using the per-service endpoint (default `http://localhost:4318` over HTTP). Grafana is the viewer, not a separate logging product.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A service team can enable shared logging on a new service and see structured local diagnostics within 1 minute of a successful start, without writing a custom logging setup.
- **SC-002**: An operator can change one service's diagnostic verbosity by editing only that service's settings and restarting it; the change is visible on the next request and does not alter any other service.
- **SC-003**: For requests that have trace context, at least 95% of diagnostic records written during the request carry the same correlation identifiers as the trace.
- **SC-004**: 100% of startup attempts succeed in writing local diagnostics when only optional remote export is missing or unreachable (invalid local configuration may still fail startup).
- **SC-005**: A support engineer can identify which service and environment produced a diagnostic record in under 10 seconds from the record alone.
- **SC-006**: Two independently configured services can run side by side with different minimum severities and produce distinguishable records for the same period of time.
- **SC-007**: With the Grafana collector running at the configured endpoint, an operator can find a service's recent diagnostic records in Grafana within 2 minutes of those records being written.

## Assumptions

- Product constraint from the request: logging uses Serilog; the shared logging building block owns setup. Observability correlation and Grafana export use OpenTelemetry. These names are constraints, not implementation steps.
- Observability integration means: enrich diagnostic records with trace correlation identifiers, and export every eligible record through OpenTelemetry to the Grafana Alloy/collector when the endpoint is set. Full metrics and a standalone tracing product are out of scope unless needed for those correlation identifiers.
- Local console-style output is always available. Grafana export uses the OpenTelemetry endpoint in each service's **base** application settings (`http://localhost:4318` over HTTP). Environment-specific files may override the URL. Export is off only when that endpoint is empty or omitted. Grafana Cloud and direct Loki ingest are out of scope.
- The operator hosts Grafana in Podman with default ports: UI `http://localhost:3000`, OTLP HTTP `http://localhost:4318`. A compose file under `deploy/observability/` is the supported local stack (Grafana LGTM all-in-one). Catalog only pushes logs; it does not start Podman.
- Catalog is the only existing service and is the first adopter. Future services reuse the same building block and settings pattern.
- Environment-specific settings files override base settings using the platform's normal settings layering.
- Safe defaults when optional values are omitted: local output, Information-level operational severity, framework noise reduced, remote export disabled.
- Invalid *required* local logging configuration fails startup with a clear error. Missing *optional* remote export does not fail startup.
- Secrets and connection strings are never written to diagnostics. Broad automated PII redaction beyond that is out of scope for this feature.
- File sinks and cloud-monitor-specific outputs other than OpenTelemetry-to-Grafana (via Alloy/collector) are out of scope for this version.
- Existing built-in logging settings in each service are superseded by this feature's logging configuration once the shared building block is enabled.
