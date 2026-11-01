# Contract: k3s Ops/Infra (live metrics)

Operator contract for FR-016 / SC-007. Not a Catalog HTTP API. Isolated `dotnet test` MUST NOT start k3s or open Grafana.

Auth: local Grafana **admin** / **admin**. Prometheus data source already in LGTM (uid `prometheus`). Do not add one. No second Grafana.

Dashboard: existing **Ops/Infra**, uid `eshop-ops-infra`, host URL `http://localhost:3000/d/eshop-ops-infra/ops-infra` via `deploy/k8s/port-forward-localhost.sh`.

## 1. Hosting is not enough

`deploy/k8s/viewer/lgtm.yaml` MUST mount:

- A Prometheus scrape file at `/otel-lgtm/prometheus.yaml`
- The existing Ops/Infra JSON at the same Grafana provision path compose uses

Bare `grafana/otel-lgtm:0.38.0` with no mounts fails this contract.

## 2. Scrape (same-namespace Services)

Prometheus inside `lgtm` scrapes Services in namespace `viewer`:

| Job | Target |
|-----|--------|
| `catalogdb` | `postgres-exporter:9187` |
| `elasticsearch` | `elasticsearch-exporter:9114` |
| `kafka` | `kafka-exporter:9308` |
| `containers` | `cadvisor:8080` |

Keep LGTM self-scrape / OTLP settings. Job `containers` MUST relabel the Kubernetes container name onto `compose_service` when it is one of: `catalogdb`, `catalogapi`, `kafka`, `connect`, `elasticsearch`, `kibana`, `kafka-ui`, `elasticsearch-exporter`, `kafka-exporter`, `postgres-exporter`.

Do **not** fork `ops-infra.json`. Do **not** add Prometheus Operator.

## 3. Overview (FR-016, SC-007)

After `lgtm` and the metric exporters have been Ready about one minute (search MAY still be starting), Ops/Infra lists:

`catalogdb`, `catalogapi`, `kafka`, `connect`, `elasticsearch`, `kibana`, `kafka-ui`, `elasticsearch-exporter`, `kafka-exporter`, `postgres-exporter`

| State | Meaning |
|-------|---------|
| Healthy | Listed container is Ready and a live series exists |
| Not healthy | Listed container is stopped or not Ready |
| Empty / “No data” | Defect |

`log-collector` and `lgtm` MUST NOT be required on this list. Accepting the board MUST NOT wait for the whole search set.

Click a Ready identity for detail on the same dashboard (store up/connections/size; Catalog request/search; Kafka broker/lag; ES heap/CPU/docs). Empty catalog is not an outage.

## 4. Fail-open

Catalog create / get-by-id succeed if `lgtm`, cadvisor, or exporters are stopped. Catalog Deployment MUST NOT init-wait on them.

## 5. Isolated tests

File-parse `deploy/k8s/viewer/` MUST assert the scrape targets, `compose_service` relabel, Ops/Infra mount, and no Catalog viewer start-gate. Tests MUST NOT start k3s, MUST NOT `kubectl apply`, MUST NOT open Grafana.
