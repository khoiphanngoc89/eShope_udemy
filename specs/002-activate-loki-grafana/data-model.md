# Data Model: Activate Loki in Grafana

This feature does not persist application entities in the repo. The model is the Grafana/Loki runtime shape operators query.

Catalog product entities (for example Create Product) and BuildingBlocks types are out of this model. They belong to a separate test-suite specification (FR-011).

## Loki Datasource

The pre-activated log source Grafana must present after a healthy stack start (FR-001, FR-010).

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| name | string | yes | `Loki` |
| type | string | yes | `loki` |
| uid | string | yes | `loki` |
| url | URI | yes | In-container Loki, `http://127.0.0.1:3100` (not a host port operators must open) |
| enabled | bool | yes | Must be selectable in Explore; operator must not add it by hand |

### States

```text
Starting → Ready → Unavailable
```

| State | Meaning | Operator sees |
|-------|---------|----------------|
| Starting | Grafana is up; Loki `/ready` not yet 200; `/tmp/ready` absent | Compose not healthy; `--wait` still blocking |
| Ready | `/tmp/ready` present; Grafana lists uid `loki`; datasource health OK | Explore → Loki works; empty search is “no matching logs” |
| Unavailable | Grafana reachable; Loki stopped or health ERROR | Grafana reports the log source is down or still failing (FR-007) |

### Validation

- First successful `compose up --wait`: state is **Ready**. No manual datasource create (FR-001, FR-010).
- Missing uid `loki` after wait: activation failed.
- Health ERROR after Ready: **Unavailable**, not a missing-datasource setup screen.

## Log Record

One diagnostic event stored in Loki and shown in Grafana. Produced by the existing shared-logging export path; this feature only requires it to be queryable (FR-003).

| Field | Type | Required | Loki / Explore mapping |
|-------|------|----------|------------------------|
| Timestamp | datetime | yes | Loki entry timestamp |
| Message | string | yes | Log line / OTLP body |
| service_name | string | yes | From OTLP `service.name` (Catalog: `eShop.Catalog.Api`) |
| Severity | string | yes | OTLP severity; live label is `detected_level` or `level` (confirm on pinned image) |
| deployment.environment | string | no | Present when the producer enriches it |
| trace_id | string | no | Present when the producer had a request activity (FR-005) |

### Rules

- Records from two services MUST remain distinguishable by `service_name`.
- A record written with no request activity stays valid and omits `trace_id`.
- This feature does not add or rename producer fields.

## Log Query

What an operator (or the contract check) sends to Loki through Grafana.

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| LogQL | string | yes | At least `{service_name=~".*eShop.Catalog.Api.*"}` on `grafana/otel-lgtm:0.38.0` |
| Time range | interval | yes | Default validation window: last 15 minutes |
| Severity filter | string | no | Label match or line filter; see research.md |
| Result | list of Log Records | yes | Empty list is success (FR-006); transport/config error is failure |

### Relationships

- One **Loki Datasource** per local stack.
- One datasource answers many **Log Queries**.
- Each query returns zero or more **Log Records**.
- **Service identity** is the `service_name` label on those records.

## Service Identity

| Field | Type | Required | Notes |
|-------|------|----------|--------|
| service_name | string | yes | Catalog first: `eShop.Catalog.Api` |

Used to isolate one service in under 30 seconds (SC-003, FR-004).
