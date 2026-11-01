# Research: Modern Grafana Log Collection

## 1. One collector: Grafana Alloy (not Promtail + Agent)

**Decision**: Add a single compose service `log-collector` using `grafana/alloy` (pin a published v1.x tag at implement). Config file under `deploy/alloy/config.alloy`. Do not run Promtail and Alloy together. Do not add Grafana Agent static mode.

**Rationale**: Spec Option 1 is “Promtail/Grafana Agent.” Alloy is the current Grafana collector that replaces both. Operators still see one service. Ponytail: one image, one config, Docker discovery + Loki write.

**Alternatives considered**:
- Promtail only — valid equivalent; rejected as the default because the user asked for the modern Grafana way and Alloy is that product now. Implement MAY fall back to Promtail with the same keep-list and push URL if Alloy is blocked.
- Grafana Agent static — superseded by Alloy.
- Serilog Loki sink on Catalog — rejected by 002; does not collect Kafka stdout.
- Docker `logging: loki` driver on each service — extra plugin, not one collector.

## 2. Loki push URL (no second Loki, host 3100 optional)

**Decision**: Collector writes to `http://lgtm:3100/loki/api/v1/push`. Loki is already inside `grafana/otel-lgtm:0.38.0`. Do not add a Loki container. Do not add a second Grafana.

**Rationale**: Phase 0 probe: from another container on network `eshop`, `http://lgtm:3100/ready` returns `ready`. Host publish of `3100` is not required for the collector. Operators query via Grafana `:3000` (uid `loki`).

**Alternatives considered**:
- Publish `3100:3100` always — useful for raw curl, not required (same as 002).
- If a future LGTM bind is `127.0.0.1` only — then publish `3100` or fix listen address; do not add a second Loki.

## 3. How logs are collected (Docker discovery, two containers)

**Decision**: Alloy `discovery.docker` against `/var/run/docker.sock` (read-only). Relabel **keep** only container names `eshop_catalog_api` and `eshop_kafka`. Set label `compose_service` from compose metadata (`catalogapi` / `kafka`). Pipeline is Docker json-file logs (stdout/stderr). Do not consume Kafka topics. Do not tail Kafka data-dir segment files.

**Rationale**: FR-001 / FR-004 / clarify 2026-11-01. Process logs only. Topic payloads stay in Kafka UI.

**Alternatives considered**:
- Bind-mount `/var/lib/docker/containers` and glob json logs — works, more host-specific than the socket.
- Collect all compose services — rejected; Connect, Kafka UI, DBs out of slice.
- Kafka client subscriber for `eshop.catalog.sync*` — that is message contents, not process logs.

## 4. Labels operators use in Explore

**Decision**: Document and contract-test these LogQL selectors (last 15 minutes):

| Need | Selector |
|------|----------|
| Catalog (collector) | `{compose_service="catalogapi"}` |
| Kafka broker process | `{compose_service="kafka"}` |
| Catalog errors (collector) | `{compose_service="catalogapi"}` plus a line filter for `Error` / `failed` |
| Existing OTLP Catalog path (002, keep) | `{service_name=~".*eShop.Catalog.Api.*"}` |

**Rationale**: FR-002, FR-009. Two identities so Catalog and Kafka do not mix. OTLP overlap is allowed.

**Alternatives considered**:
- Reuse only `service_name` — collector stdout will not have OTLP resource attributes unless we parse JSON; compose_service is reliable.
- A new Grafana dashboard — Explore is enough for this slice (002 style). Ops/Infra and Log/App stay 006.

## 5. Fail-open and tests

**Decision**: `catalogapi` still `depends_on` `catalogdb` only. `kafka` does not `depends_on` `log-collector`. Isolated `dotnet test` parses compose + collector config (keep-list). No Testcontainers. No Catalog C# change unless a one-line comment. Do not edit `BuildBlock.Logging.Tests`.

**Rationale**: FR-005, FR-006, SC-003, SC-004. Catalog already prints failed-request Errors to console (005 behavior) — collector tails that.

**Alternatives considered**:
- Catalog `depends_on` collector — rejected by FR-005.
- New logging project — rejected (abstraction ban).

## 6. Compose layout

**Decision**: Extend `deploy/compose.yaml` + `deploy/compose.override.yaml` only. Collector may `depends_on` `lgtm` healthy so push has a target. README: start `lgtm kafka catalogapi log-collector` for the full Loki path.

**Rationale**: FR-007, FR-008. Keep 006 exporter/Grafana mounts as they are.

**Alternatives considered**: Second compose file — rejected.
