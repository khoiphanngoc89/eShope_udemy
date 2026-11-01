# Contract: Compose hybrid stack

Operator and compose contract. `dotnet test` does not start this stack.

## Services (FR-009)

Extend `deploy/compose.yaml` + `deploy/compose.override.yaml` only.

| Service | Image / role | Published |
|---------|--------------|-----------|
| `elasticsearch-exporter` | prometheuscommunity elasticsearch-exporter | `9114` (optional host) |
| `lgtm` | existing Grafana LGTM | `3000`, `4317`, `4318` |
| `elasticsearch` | existing ES 8 | `9200` |

New files under `deploy/` (not a second compose file), for example:

- `deploy/prometheus/prometheus.yaml` — LGTM default jobs **plus** scrape `elasticsearch-exporter:9114`
- `deploy/grafana/provisioning/datasources/elasticsearch.yaml`
- `deploy/grafana/provisioning/dashboards/` — Ops/Infra + Log/App
- `deploy/grafana/provisioning/alerting/` — CPU, heap, red-cluster rules

## Start

```bash
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait \
  elasticsearch elasticsearch-exporter lgtm catalogapi
```

Catalog may start without exporter or lgtm. Exporter waits for Elasticsearch healthy.

## Success

- `GET http://localhost:9114/metrics` includes `elasticsearch_cluster_health_` series when ES is up.
- Grafana `http://localhost:3000` shows Prometheus and Elasticsearch data sources and both dashboards.
- `catalogapi` health does not wait on exporter or lgtm.

## Failure of the feature

- Second compose file or second Grafana.
- `catalogapi` `depends_on` exporter or lgtm.
- Mounted Prometheus config drops LGTM’s existing scrape jobs.
