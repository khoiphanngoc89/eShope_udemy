# Data Model: Local k3s (WSL2)

No new Catalog HTTP body. These are **cluster objects** the operator applies. Product rows still live in Postgres via existing Catalog APIs.

## Namespaces

One k3s cluster. Three namespaces. No NetworkPolicy.

| Name | Workloads |
|------|-----------|
| `catalog` | `catalogdb`, `catalogapi`, PVC `catalogdb-data` |
| `search` | `kafka`, `connect`, `connect-register`, `elasticsearch`, `kibana`, `kafka-ui` |
| `viewer` | `lgtm`, `log-collector`, `postgres-exporter`, `elasticsearch-exporter`, `kafka-exporter`, `cadvisor` |

**DNS**:
- Same namespace: short Service name (`catalogdb`, `kafka`).
- Cross namespace: `name.namespace.svc` (example `kafka.search.svc:9092`).

## PersistentVolumeClaim `catalogdb-data`

| Field | Rule |
|-------|------|
| Name | `catalogdb-data` |
| Namespace | `catalog` |
| Access | `ReadWriteOnce` |
| Size | at least `1Gi` (classroom) |
| StorageClass | k3s `local-path` (cluster default) |
| Mount | catalogdb container path `/var/lib/postgresql/data` |

**Must not** reference Docker volume `catalog-pgdata` or a host bind of that volume.

**Lifecycle**:
- Ordinary stop (stop port-forward / `sudo systemctl stop k3s`) → PVC and data remain.
- Destructive stop (`kubectl delete pvc catalogdb-data -n catalog` or wipe k3s storage) → cluster catalog data gone. Compose `catalog-pgdata` is unused.

## Deployment + Service `catalogdb`

| Field | Rule |
|-------|------|
| Namespace | `catalog` |
| Image | `postgres:16.15` |
| Replicas | 1 |
| Env | `POSTGRES_USER=catalog`, `POSTGRES_PASSWORD=catalog`, `POSTGRES_DB=catalog` (same as compose) |
| Command extra | `postgres -c wal_level=logical` (same as compose; required for Debezium CDC) |
| Port | 5432 |
| Service | ClusterIP `catalogdb:5432` (FQDN from other ns: `catalogdb.catalog.svc:5432`) |
| Readiness | `pg_isready -U catalog -d catalog` |
| Volume | PVC `catalogdb-data` |

**Relationship**: Catalog’s only start-gate dependency.

## Deployment + Service `catalogapi`

| Field | Rule |
|-------|------|
| Namespace | `catalog` |
| Image | `eshop-catalog-api:local` |
| imagePullPolicy | `Never` |
| Replicas | 1 |
| Container port | 8080 (same as Dockerfile `ASPNETCORE_URLS=http://+:8080`) |
| Service | ClusterIP `catalogapi:8080` |
| Host URL | `deploy/k8s/port-forward-localhost.sh` → `http://localhost:5000` |
| Connection string | `Host=catalogdb;Port=5432;Database=catalog;Username=catalog;Password=catalog` (same namespace) |
| Optional env (fail-open) | `Elasticsearch__Uri=http://elasticsearch.search.svc:9200`, `Kafka__BootstrapServers=kafka.search.svc:9092`, `OTEL_EXPORTER_OTLP_ENDPOINT=http://lgtm.viewer.svc:4318` |
| Init | wait until store accepts connections |
| Readiness | HTTP or TCP on 8080 |
| Start gate | store only — no wait on lgtm / kafka / elasticsearch |

**Relationship**: Talks to `catalogdb` in the same namespace. Search-sync in `search` reaches Catalog/store via `catalogapi.catalog.svc` / `catalogdb.catalog.svc`.

## Search-sync (official apply)

Same images as `deploy/compose.yaml`. Namespace `search`. **Must not** be a Catalog Ready condition.

| Object | Image | In-cluster |
|--------|-------|------------|
| `kafka` | `apache/kafka:3.8.1` | `kafka:9092` (from `catalog`: `kafka.search.svc:9092`) |
| `connect` | `quay.io/debezium/connect:2.7.3.Final` | `connect:8083`; store host `catalogdb.catalog.svc` |
| `connect-register` | `quay.io/debezium/connect:2.7.3.Final` | Job/Pod registers the compose connector |
| `elasticsearch` | `docker.elastic.co/elasticsearch/elasticsearch:8.15.3` | `elasticsearch:9200` (from `catalog`: `elasticsearch.search.svc:9200`) |
| `kibana` | `docker.elastic.co/kibana/kibana:8.15.3` | `kibana:5601` |
| `kafka-ui` | `provectuslabs/kafka-ui:v0.7.2` | `kafka-ui:8080` |

## Viewer, collector, exporters (official apply)

Same images as compose. Namespace `viewer`. **Must not** be a Catalog Ready condition. No second Grafana product.

| Object | Image | Cross-ns target |
|--------|-------|-----------------|
| `lgtm` | `grafana/otel-lgtm:0.38.0` | `lgtm.viewer.svc:4318` / `:3000`. MUST mount k3s Prometheus scrape + existing Ops/Infra dashboard (same in-container paths as compose). |
| `log-collector` | `grafana/alloy:v1.21.3` | Discover pods in `catalog`, `search`, `viewer` (not compose docker.sock) |
| `postgres-exporter` | `quay.io/prometheuscommunity/postgres-exporter:v0.17.1` | `catalogdb.catalog.svc:5432` |
| `elasticsearch-exporter` | `quay.io/prometheuscommunity/elasticsearch-exporter:v1.8.0` | `elasticsearch.search.svc:9200` |
| `kafka-exporter` | `danielqsj/kafka-exporter:v1.11.0` | `kafka.search.svc:9092` |
| `cadvisor` | `ghcr.io/google/cadvisor:v0.60.6` | k3s containerd socket `/run/k3s/containerd/containerd.sock` |

## Ops/Infra (k3s)

Same Grafana view as compose. Not a second product. Not a Catalog Ready gate.

| Field | Rule |
|-------|------|
| Dashboard | Existing Ops/Infra, uid `eshop-ops-infra` |
| Data source | Prometheus already in LGTM (uid `prometheus`). Operator MUST NOT add one. |
| Overview set | `catalogdb`, `catalogapi`, `kafka`, `connect`, `elasticsearch`, `kibana`, `kafka-ui`, `elasticsearch-exporter`, `kafka-exporter`, `postgres-exporter` |
| Off the board | `log-collector`, `lgtm` |
| Healthy | Listed container is Ready and a running-container / scrape series exists |
| Not healthy | Listed container is stopped or not Ready (this is data, not an empty panel) |
| Empty / “No data” | Defect after viewer + exporters Ready about one minute |
| Search still starting | Board MAY pass if not-ready listed identities show not healthy |
| Scrape jobs | `catalogdb` → `postgres-exporter:9187`; `elasticsearch` → `elasticsearch-exporter:9114`; `kafka` → `kafka-exporter:9308`; `containers` → `cadvisor:8080` |
| Label bridge | cadvisor job relabels Kubernetes container name → `compose_service` for the overview set |

**Relationship**: Prometheus inside `lgtm` scrapes Services in the same `viewer` namespace. Exporters already talk cross-ns to `catalogdb.catalog.svc`, `elasticsearch.search.svc`, `kafka.search.svc`.

## Validation (isolated tests)

Manifests MUST encode:
- three namespaces `catalog`, `search`, `viewer`
- images and same-ns store host `catalogdb`
- cross-ns hosts `kafka.search.svc`, `elasticsearch.search.svc`, `lgtm.viewer.svc`, `catalogdb.catalog.svc` where those calls leave the namespace
- no `catalog-pgdata` bind
- Catalog init wait / no viewer or search wait
- Root `deploy/k8s/kustomization.yaml` includes catalog, search, and viewer
- README documents k3s in Ubuntu, `dotnet publish` + `k3s ctr images import`, official apply `-k deploy/k8s`, and `deploy/k8s/port-forward-localhost.sh` / `5000:8080`
- README MUST NOT require Docker, Podman, minikube, k3d, or `docker compose`
- `lgtm` mounts a Prometheus scrape file with targets `postgres-exporter:9187`, `elasticsearch-exporter:9114`, `kafka-exporter:9308`, `cadvisor:8080` and a `compose_service` relabel; Ops/Infra dashboard file is mounted; Catalog still has no viewer start-gate
