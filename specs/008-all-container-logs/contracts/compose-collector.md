# Contract: Compose collector + health exporters

Operator and compose contract. `dotnet test` does not start this stack.

## Services (FR-007)

Extend `deploy/compose.yaml` + `deploy/compose.override.yaml` only.

| Service | Image / role | Published |
|---------|--------------|-----------|
| `log-collector` | existing `grafana/alloy:v1.21.3` | none required |
| `postgres-exporter` | `quay.io/prometheuscommunity/postgres-exporter` (pin v0.17+ at implement) | `9187` (optional host) |
| `cadvisor` | `gcr.io/cadvisor/cadvisor` or `ghcr.io/google/cadvisor` (pin v0.49+ at implement) | `8081` optional host if 8080 clashes |
| `lgtm` | existing Grafana LGTM | `3000`, `4317`, `4318` |
| `catalogapi` | existing Catalog | `5000` |
| `catalogdb` | existing store | `5432` |

Changed files under `deploy/` (not a second compose file):

- `deploy/alloy/config.alloy` — Docker discovery on network `eshop`; drop `catalogapi`, `log-collector`, `lgtm`; `compose_service` from compose metadata; Loki write `http://lgtm:3100/loki/api/v1/push`
- `deploy/prometheus/prometheus.yaml` — existing jobs **plus** `catalogdb` → `postgres-exporter:9187` **plus** `containers` → `cadvisor:8080`
- `deploy/grafana/provisioning/dashboards/ops-infra.json` — product-stack overview + click-through `$service` detail (uid `eshop-ops-infra`)

## Depends on (FR-005, FR-013)

| Service | May wait for | Must **not** wait for |
|---------|--------------|------------------------|
| `log-collector` | `lgtm` healthy | Catalog, Kafka, store, exporters, cadvisor |
| `postgres-exporter` | `catalogdb` healthy | Catalog, `lgtm`, `log-collector`, cadvisor |
| `cadvisor` | (none required) | Catalog, `lgtm` |
| `catalogapi` | `catalogdb` only | `log-collector`, `lgtm`, `postgres-exporter`, `cadvisor` |
| `kafka` | (existing only) | `log-collector`, `lgtm`, `postgres-exporter`, `cadvisor` |

## Start

```bash
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait \
  catalogdb postgres-exporter cadvisor lgtm kafka catalogapi log-collector
```

Catalog and Kafka may start without the collector, Grafana, cadvisor, or the store exporter.

## Success

- Grafana Explore → Loki lists without adding a data source.
- After each non-app target is healthy and has written stdout, `{compose_service="<service>"}` returns that container’s process stream.
- Alloy drop-list includes `catalogapi` (and `log-collector` / `lgtm`). Config does **not** subscribe to Kafka topics.
- Prometheus scrapes cadvisor and postgres-exporter. No `stat_statements` query-text series.
- Ops/Infra uid `eshop-ops-infra` is the one overview + detail dashboard. `log-collector` and `lgtm` are not required identities.

## Failure of the feature

- Second compose file or second Grafana / Loki / second metrics dashboard uid used as the operator home.
- `catalogapi` or `kafka` `depends_on` `log-collector`, `lgtm`, `postgres-exporter`, or `cadvisor`.
- Collector consumes Kafka topics or tails Kafka data-dir segments.
- Collector ships `catalogapi` stdout.
- Catalog scrapes the store or cadvisor.
- Store detail exposes SQL statement text or per-query payloads.
- Mounted Prometheus config drops LGTM’s existing scrape jobs.
