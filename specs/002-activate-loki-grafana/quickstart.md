# Quickstart: Activate Loki in Grafana

Validation guide for `002-activate-loki-grafana`. Implementation steps belong in `tasks.md` (not created by `/speckit-plan`).

## Prerequisites

- Podman or Docker with Compose
- Repo file: [deploy/observability/compose.yaml](../../deploy/observability/compose.yaml)
- Spec and contracts:
  - [spec.md](./spec.md)
  - [contracts/grafana-loki-api.md](./contracts/grafana-loki-api.md)
  - [data-model.md](./data-model.md)
- Catalog logging from `001-serilog-otel-logging` is already enabled (OTLP `http://localhost:4318`). This feature does not change that.

## Start the stack (must wait until Loki is ready)

From the repo root:

```bash
podman compose -f deploy/observability/compose.yaml up -d --wait
# or: docker compose -f deploy/observability/compose.yaml up -d --wait
```

Expected: the command returns only after the LGTM service is healthy (`/tmp/ready` present). Grafana UI: `http://localhost:3000` (admin / admin).

## Prove Loki is activated (no Catalog required)

```bash
curl -fsS -u admin:admin http://localhost:3000/api/health
curl -fsS -u admin:admin http://localhost:3000/api/datasources
curl -fsS -u admin:admin http://localhost:3000/api/datasources/uid/loki
curl -fsS -u admin:admin http://localhost:3000/api/datasources/uid/loki/health
```

| Check | Expected |
|-------|----------|
| `/api/health` | HTTP 200 |
| `/api/datasources` | JSON includes `name=Loki`, `type=loki`, `uid=loki` |
| `/api/datasources/uid/loki` | HTTP 200, same identity |
| `/api/datasources/uid/loki/health` | HTTP 200, status OK |
| Operator UI | Explore → select **Loki** → run a last-15-minute search without adding a data source |

Empty-window query (Catalog off is fine):

Use Grafana’s Loki proxy range query from [contracts/grafana-loki-api.md](./contracts/grafana-loki-api.md):

```bash
NOW=$(date +%s)000
START=$((NOW - 900000))
curl -sS -u admin:admin -G \
  --data-urlencode 'query={service_name=~".*eShop.Catalog.Api.*"}' \
  --data-urlencode "start=${START}000000" \
  --data-urlencode "end=${NOW}000000" \
  --data-urlencode 'limit=10' \
  'http://localhost:3000/api/datasources/proxy/uid/loki/loki/api/v1/query_range'
```

Expected: HTTP 200, `"status":"success"`, **zero rows** (`result: []`), not a connection error.

## Prove Catalog logs appear (existing export path)

1. Leave the stack up.
2. Run Catalog (`dotnet run --project src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj`).
3. Hit an existing Catalog route (for example `POST /products`).
4. In Grafana Explore → Loki, query `{service_name=~".*eShop.Catalog.Api.*"}` for the last 15 minutes.

Expected within 2 minutes:

- Rows for `eShop.Catalog.Api`
- Other services (if any) excluded by that selector
- Request-time rows can show `trace_id` when the producer had an activity

## Prove Loki-not-ready is obvious

With Grafana still reachable, stop Loki or the whole container and open Explore → Loki (or call `/api/datasources/uid/loki/health`).

Expected: Grafana reports the log source unavailable / ERROR — not a blank page and not a “create data source” wizard.

Restart with `up -d --wait`. The same Loki uid works again with no manual setup.

## Out of scope for this validation

- Changing Catalog or `BuildBlock.Logging` settings
- Grafana Cloud, a second Loki, or a new collector
- Dashboards and alert rules
- Publishing Loki `:3100` on the host
