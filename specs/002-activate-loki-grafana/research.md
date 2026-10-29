# Research: Activate Loki in Grafana

## 1. Keep the existing LGTM container vs split Loki out

**Decision**: Keep the single `grafana/otel-lgtm` service (`lgtm`) in the project compose pair `deploy/compose.yaml` + `deploy/compose.override.yaml`. Do not add a separate Loki, Grafana, Alloy, or collector service.

**Rationale**: The logging feature already chose this image as the local stack. It already runs Loki, Grafana, and an OpenTelemetry collector that writes OTLP logs into Loki. Splitting the stack would change how operators start Grafana and would risk breaking Catalog’s existing `http://localhost:4318` export (FR-008, FR-009).

**Alternatives considered**:
- Compose services for Grafana + Loki + Alloy — more files, more ports, duplicates what LGTM already does.
- Grafana Cloud or a remote Loki — out of scope (spec assumptions).
- Direct Loki push from Catalog — rejected by the logging spec and FR-008.

## 2. What “activate Loki” means in this repo

**Decision**: Loki is already bundled and pre-provisioned inside `grafana/otel-lgtm` (datasource name `Loki`, uid `loki`, URL `http://127.0.0.1:3100`). Activation in this repo means: the compose service is not treated as ready until Loki is ready, Grafana lists that datasource without operator setup, and the operator path to query Catalog logs is documented and contract-tested.

**Rationale**: The current compose file only publishes ports. The image’s own startup script waits for Loki `/ready` and Grafana `/api/health` before writing `/tmp/ready`, but compose does not wait on that. Operators can open Grafana before Loki is selectable or healthy, which fails FR-001 / FR-007 / SC-001.

**Alternatives considered**:
- Copy Grafana datasource YAML into the repo and mount it — duplicates the image’s `grafana-datasources.yaml` and can break Tempo/Prometheus links if it drifts.
- Set Loki as Grafana’s default datasource — steals the default from metrics; Explore already lets the operator pick Loki (US1).
- Publish Loki `:3100` on the host — useful for raw curl, not required; operators use Grafana (FR-002, FR-009).

## 3. How service logs reach Loki

**Decision**: Do not change Catalog or `BuildBlock.Logging`. Catalog already exports OTLP HTTP to `http://localhost:4318`. The LGTM collector receives that traffic and writes logs to the in-container Loki. Grafana queries Loki; Catalog never talks to Loki.

**Rationale**: FR-008. The existing pipeline is the product constraint from `001-serilog-otel-logging`.

**Alternatives considered**:
- Add a Loki sink in Serilog — new service config, rejected by FR-008.
- Point Catalog at Loki’s native ingest API — bypasses the collector path the logging spec requires.

## 4. Query labels operators use in Explore

**Decision**: Document and contract-test these LogQL selectors against the LGTM Loki:

| Need | Selector | Source |
|------|----------|--------|
| Service identity | `{service_name="eShop.Catalog.Api"}` | OTLP resource `service.name` → Loki `service_name` |
| Severity | `{service_name="…", detected_level="error"}` or `{service_name="…"} \|= "Error"` | OTLP severity; exact label name confirmed against the pinned image at implement time |
| Time range | Grafana Explore range (last 15 minutes) | Grafana query window |
| Trace correlation | inspect `trace_id` on the row (LGTM maps it as a Loki label / derived field) | OTLP trace id from Serilog’s `TraceId` |

If the pinned image uses `level` instead of `detected_level`, the contract records the live label. Do not invent a custom label pipeline.

**Rationale**: LGTM’s Tempo→Loki link already uses `service.name` → `service_name`. Grafana Labs documents Explore → Loki + LogQL for this image. That satisfies FR-004 / FR-005 with zero collector config in this repo.

**Alternatives considered**:
- Custom Alloy relabel rules — extra service, not needed.
- JSON parse of every line (`| json | line_format "{{.body}}"`) as the only query — useful as a fallback for older Loki exporter JSON, not the primary documented query once Loki 3 OTel ingest is in the image.

## 5. Readiness and “Loki not ready” (FR-007)

**Decision**:
- Compose `healthcheck` succeeds only when the image’s ready marker exists (`/tmp/ready`), which the image writes after Loki `http://127.0.0.1:3100/ready` and Grafana `http://127.0.0.1:3000/api/health` both return 200.
- Operators start with `podman compose … up -d --wait` (or Docker equivalent) so “stack is up” means Loki is queryable.
- Do not build a custom Grafana error page. Grafana’s built-in datasource health / Explore error is the unavailable state when Loki later dies.

**Rationale**: Reuses the image’s own readiness contract. Smallest compose change that makes FR-001 and FR-007 testable from the host.

**Alternatives considered**:
- Host-side poll of unpublished `:3100` — port not exposed; would require publishing it.
- Only check Grafana `:3000` — Grafana can be up while Loki is still starting (the bug this feature fixes).
- Custom frontend copy in Grafana — out of scope.

## 6. Image tag

**Decision**: Pin `grafana/otel-lgtm` to published tag `0.38.0`. Stop using `:latest`.

**Rationale**: `:latest` can drop or rename the Loki datasource and break the contract. A pin keeps FR-001/FR-010 stable. `0.38.0` is the tag already running for service `lgtm`.

**Alternatives considered**:
- Keep `:latest` — rejected; activation must survive a pull.
- Vendor a custom image — rejected; no justification.

## 7. Testing

**Decision**: No new .NET project and no C# production code. Validation is HTTP against Grafana (see [contracts/grafana-loki-api.md](./contracts/grafana-loki-api.md)):

1. After `--wait`, Loki appears in `GET /api/datasources` as type `loki`, uid `loki`.
2. `GET /api/datasources/uid/loki/health` is OK.
3. A Loki range query for a window with no Catalog traffic returns success with empty results (not a connection error).
4. After Catalog is exporting, `{service_name=~".*eShop.Catalog.Api.*"}` returns rows within 2 minutes (pinned image stores `unknown_service:eShop.Catalog.Api.exe`).

TDD for this feature: write/run those HTTP checks first (they fail if compose has no health wait / Loki is missing). Then change compose. Production C# stays untouched.

**Rationale**: The workspace TDD rule applies to C# production logic. This feature is deploy config. A second test host would be a new layer with nothing to unit-test. Session 2026-10-27 / FR-011: Catalog.Api and BuildingBlocks unit/integration test suites are a separate feature and MUST NOT be added here.

**Alternatives considered**:
- xUnit tests that skip when Grafana is down — extra project, weak signal in CI without the stack.
- Add `eShop.Catalog.Api` / BuildingBlocks test projects in this feature — rejected by FR-011; those suites need their own specification.
- Only a prose README — not testable enough for FR-001.

## 8. Auth and scope leftovers

**Decision**: Reuse Grafana `admin` / `admin` already documented for this stack. No new login, no anonymous-auth change, no dashboards, no alert rules.

**Rationale**: Spec assumptions. Ponytail: do not add what the spec excluded.
