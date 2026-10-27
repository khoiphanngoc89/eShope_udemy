# Contract: Grafana Loki activation APIs

Operators use Grafana’s UI. These HTTP calls are the machine-checkable contract that Loki is activated. Base URL: `http://localhost:3000`. Auth: HTTP Basic `admin` / `admin` (existing local stack credentials).

Do not call Loki on the host. Loki listens only inside the LGTM container; Grafana proxies queries.

## 1. Stack health

```http
GET /api/health
```

**When compose is Ready**

- Status: `200`
- Body includes Grafana database status `ok` (field names follow the running Grafana version).

**When Grafana is still starting**

- Connection refused or non-200. Compose `--wait` must not finish.

## 2. Loki is listed (FR-001)

```http
GET /api/datasources
```

**When compose is Ready**

- Status: `200`
- Body is a JSON array that includes one element matching:

| Field | Value |
|-------|--------|
| `name` | `Loki` |
| `type` | `loki` |
| `uid` | `loki` |

The operator must not have created this row.

```http
GET /api/datasources/uid/loki
```

- Status: `200`
- Same `name` / `type` / `uid` as above.

**Failure**

- `404` or no `type=loki` row → Loki is not activated.

## 3. Loki is healthy (Ready vs Unavailable)

```http
GET /api/datasources/uid/loki/health
```

**Ready**

- Status: `200`
- Body `status` is `OK` (or equivalent success string for that Grafana version).

**Unavailable (FR-007)**

- Grafana itself may still return `200` on `/api/health`.
- This health call is non-200 or `status` is `ERROR`.
- Explore shows a datasource error, not a setup wizard.

## 4. Empty search is not an error (FR-006)

Query Loki through Grafana (either form is acceptable; pick one at implement time and keep it in the quickstart):

```http
POST /api/ds/query
```

or Grafana’s Loki proxy:

```http
GET /api/datasources/proxy/uid/loki/loki/api/v1/query_range
```

**Request (conceptual)**

- LogQL: `{service_name=~".*eShop.Catalog.Api.*"}` (pinned `grafana/otel-lgtm:0.38.0` stores OTLP `service_name` as `unknown_service:eShop.Catalog.Api.exe`)
- Range: last 15 minutes
- Stack Ready, Catalog not exporting (or no rows in range)

**Success**

- Transport status `200`
- Query status success
- Zero log rows
- Not a datasource / connection / parse-config error

## 5. Catalog records appear (FR-003, FR-004, FR-005)

Same query as §4 after Catalog has been running with its existing OTLP export (`http://localhost:4318`).

**Success (within 2 minutes of those records being written)**

- At least one row
- Rows are filterable by `service_name="eShop.Catalog.Api"`
- When the producer had a request activity, `trace_id` is visible on the row

**Out of scope for this contract**

- Creating or updating datasources via `POST /api/datasources`
- Grafana Cloud tokens
- Alerting / dashboard APIs
- Changing Catalog `Serilog` settings
- New unit or integration test projects for `eShop.Catalog.Api` or BuildingBlocks (FR-011)
