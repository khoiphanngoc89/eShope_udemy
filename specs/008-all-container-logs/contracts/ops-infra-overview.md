# Contract: Ops/Infra stack-health overview

Operator contract. Not a Catalog HTTP API. `dotnet test` does not start Grafana, cadvisor, or the exporters.

Auth: local Grafana **admin** / **admin**. Prometheus data source already provisioned (uid `prometheus`). Do not add one.

Dashboard: existing **Ops/Infra**, uid `eshop-ops-infra`. Same Grafana. No second dashboard as the operator home.

## 1. Overview (FR-012, SC-007)

After `lgtm` and `cadvisor` are up, the top of Ops/Infra lists these identities **even if some are not started**:

`catalogdb`, `catalogapi`, `kafka`, `connect`, `elasticsearch`, `kibana`, `kafka-ui`, `elasticsearch-exporter`, `kafka-exporter`, `postgres-exporter`

Each shows **healthy** (process up — a running-container series for that `compose_service`) or **not healthy** (stopped or missing). `log-collector` and `lgtm` MUST NOT be required on this list.

**Success**: operator can tell healthy from not-healthy in under 2 minutes without adding a data source.

**Failure of the feature**: operator must add a Prometheus data source; a missing product-stack container is omitted so it cannot look not-healthy; collector or Grafana is required as an overview identity.

## 2. Click for detail (FR-012)

Clicking an overview identity opens **that container’s detail on the same dashboard** (template variable `service`, Grafana data link `var-service=<compose_service>`).

| `service` | Detail the operator must see |
|-----------|------------------------------|
| every listed id | CPU and RAM |
| `catalogdb` | Store up, connections, size ([§3](#3-store-detail-fr-012-fr-014-sc-006)), plus CPU and RAM |
| `catalogapi` | Existing Catalog.Api request / search / sync signals, plus CPU and RAM |
| `kafka` | Existing Kafka broker / topic / lag signals (not topic payloads), plus CPU and RAM |
| `elasticsearch` | Existing ES cluster / heap / CPU / docs signals (docs 0 is not cluster-down), plus container CPU and RAM |

**Success**: one click, same Grafana, no second viewer.

## 3. Store detail (FR-012, FR-014, SC-006)

When `service=catalogdb` and the store has been up about one minute:

```promql
pg_up
pg_stat_database_numbackends{datname="catalog"}
pg_database_size_bytes{datname="catalog"}
```

`up{job="catalogdb"}` MAY stand in for `pg_up` if the pin only exposes scrape success.

**Success**: operator sees up / connections / size in under 2 minutes. Small or zero size is **not** not-healthy and is **not** an outage. No SQL text or per-query payloads.

## 4. Fail-open (FR-013)

Catalog create / get-by-id succeed if `cadvisor`, `postgres-exporter`, or Grafana is stopped.
