# Contract: Compose log collector

Operator and compose contract. `dotnet test` does not start this stack.

## Services (FR-007)

Extend `deploy/compose.yaml` + `deploy/compose.override.yaml` only.

| Service | Image / role | Published |
|---------|--------------|-----------|
| `log-collector` | `grafana/alloy` (pinned v1.x) | none required |
| `lgtm` | existing Grafana LGTM | `3000`, `4317`, `4318` |
| `catalogapi` | existing Catalog | `5000` |
| `kafka` | existing broker | `9092` |

New files under `deploy/` (not a second compose file):

- `deploy/alloy/config.alloy` — Docker discovery; keep `eshop_catalog_api` and `eshop_kafka`; write `http://lgtm:3100/loki/api/v1/push`

## Depends on (FR-005)

| Service | May wait for | Must **not** wait for |
|---------|--------------|------------------------|
| `log-collector` | `lgtm` healthy | Catalog, Kafka (may start first; empty until targets exist) |
| `catalogapi` | `catalogdb` only | `log-collector`, `lgtm` |
| `kafka` | (existing only) | `log-collector`, `lgtm` |

## Start

```bash
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait \
  lgtm kafka catalogapi log-collector
```

Catalog and Kafka may start without the collector.

## Success

- Grafana Explore → Loki lists without adding a data source.
- `{compose_service="catalogapi"}` and `{compose_service="kafka"}` return the matching process streams after traffic / broker healthy.
- Collector config keep-list does not include Connect, Kafka UI, or databases.

## Failure of the feature

- Second compose file or second Grafana / Loki.
- `catalogapi` or `kafka` `depends_on` `log-collector`.
- Collector consumes Kafka topics or tails Kafka data-dir segments.
- Collector ships every compose container.
