# eShop

Local .NET e-commerce backend. Today it is one vertical: **Catalog** (create, update, get, and page/search products), shared building blocks, PostgreSQL, and optional Grafana (Ops/Infra + Log/App) plus Loki.

Work is spec-driven (`specs/`). Catalog’s documented run path is **compose**, not `dotnet run`. Isolated tests do not need Postgres, Grafana, or the Elasticsearch exporter.

**Stack:** .NET 10, Carter, MediatR, Marten, Serilog → OTLP, PostgreSQL 16.15, Grafana LGTM 0.38.0, elasticsearch-exporter v1.8.0.

Solution: `eShop.slnx`.

---

## What it does

A client `POST`s a product to Catalog.Api. Catalog stores it in PostgreSQL through Marten and returns `201` with a new `id`. Clients can `GET /products` (page plus optional name, price, or category search **from Elasticsearch**) and `GET /products/{id}` (**from Elasticsearch**), `PUT /products/{id}`, and `POST /products/import`. Create, update, and import do **not** write Elasticsearch. Debezium snapshots then streams `mt_doc_product` onto Kafka topics prefixed `eshop.catalog.sync`; Catalog’s indexer upserts `catalog-products`. If Elasticsearch is down, `GET /products` and `GET /products/{id}` return **503**; create, update, and import still work. If the index does not exist yet (no document has been synced), `GET /products` is **200** with empty `items` and `GET /products/{id}` is **404**, not 503. Request logs go to the console and, when LGTM is up, to Loki. Compose also dual-writes Catalog **Error/Fatal** logs to Elasticsearch index `catalog-app-logs` (fail-open). Correlation IDs (`trace_id`, `span_id`) are attached when ASP.NET has a request activity. There is no identity, basket, or ordering service yet.

```
Client  →  catalogapi :8080 (published :5000)  →  catalogdb :5432 (Marten)
                      │                            │
                      │                            └── Debezium → kafka → indexer → elasticsearch
                      ├── kafka-ui :8080  (filter eshop.catalog.sync)
                      ├── kibana :5601    (index catalog-products)
                      ├── elasticsearch-exporter :9114 (cluster metrics)
                      └── lgtm :4318 (Grafana :3000 Ops/Infra + Log/App, Loki, Tempo)
```

---

## Layout

```
eShop.slnx
src/
  BuildingBlocks/
    BuildingBlock/          CQRS (ICommand / IQuery) + AddAppCarter()
    BuildBlock.Logging/     Serilog + OTLP; AddAppLogging()
  Services/
    eShop.Catalog.Api/      HTTP API, Marten, product create/update/get/browse
tests/                      Isolated suites (no live Postgres, no Grafana)
deploy/
  compose.yaml              Service names and images
  compose.override.yaml     Ports, env, healthchecks, Catalog build
  k8s/                      k3s kustomize (catalog + search + viewer)
  prometheus/               Extended LGTM Prometheus scrape (ES + Kafka exporters)
  grafana/provisioning/     Ops/Infra + Log/App dashboards and alerts
specs/                      Feature specs, plans, contracts
```

| Project | Role |
|---------|------|
| `BuildingBlock` | MediatR command/query markers; Carter module registration |
| `BuildBlock.Logging` | Shared logging. Each service owns `Serilog` in its own `appsettings.json` |
| `eShop.Catalog.Api` | Public Catalog HTTP API |

Catalog wiring: `Program.cs` → `AddCatalogServices()` / `UsePipline()`. Endpoints are Carter modules (for example `CreateProductEndpoint`). Persistence is Marten lightweight sessions; schema is `AutoCreate.CreateOrUpdate` on first write. Required setting: `ConnectionStrings:CatalogDb`.

---

## NuGet

Packages come from **nuget.org**. There is no `nuget.config` and no private feed. Versions live in each `.csproj` (not central package management).

Restore from the repo root (Windows or WSL):

```bash
dotnet restore eShop.slnx
```

`dotnet build`, `dotnet test`, and `dotnet publish` restore automatically if needed.

| Project | Package | Version | Role |
|---------|---------|---------|------|
| `BuildingBlock` | Carter | 10.0.0 | HTTP modules |
| | MediatR | 12.5.0 | Commands / queries |
| | Marten | 9.54.2 | Catalog store (PostgreSQL) |
| | FluentValidation | 12.2.0 | Request validation |
| | Mapster | 10.0.13 | DTO mapping |
| | Elastic.Clients.Elasticsearch | 8.19.13 | Search index client |
| | Confluent.Kafka | 2.12.0 | Search-sync consumer |
| | OpenTelemetry.Extensions.Hosting | 1.20.1 | Metrics / traces host |
| | OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.20.1 | OTLP export |
| | OpenTelemetry.Instrumentation.AspNetCore | 1.20.1 | ASP.NET metrics |
| | OpenTelemetry.Instrumentation.Http | 1.20.1 | HTTP client metrics |
| `BuildBlock.Logging` | Serilog.AspNetCore | 9.0.0 | Request logging |
| | Serilog.Enrichers.Environment | 3.0.1 | Environment enricher |
| | Serilog.Sinks.OpenTelemetry | 4.2.0 | Logs → OTLP / Loki |
| `eShop.Catalog.Api` | Marten | 9.54.2 | Product documents |
| | Elastic.Serilog.Sinks | 8.18.2 | Catalog Error/Fatal → ES |
| | Microsoft.AspNetCore.OpenApi | 10.0.1 | OpenAPI |
| | Microsoft.OpenApi | 2.7.5 | OpenAPI model |
| | Swashbuckle.AspNetCore.SwaggerUI | 10.3.1 | Swagger UI |
| Tests | xunit | 2.9.3 | Isolated tests |
| | NSubstitute | 5.3.0 | Catalog test substitutes |
| | Microsoft.AspNetCore.Mvc.Testing | 10.0.1 | `WebApplicationFactory` |

Target framework: **net10.0**. Isolated tests do not restore a private Catalog package.

---

## Run locally

Compose files in `deploy/` stay as the service identity list. Running compose is not required. Official local run is k3s in Ubuntu (below).

From the repo root, the compose files still describe this stack:

```bash
# Store only
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait catalogdb

# Catalog.Api (starts catalogdb; Grafana not required)
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait catalogapi

# Search sync (Kafka, Debezium, Elasticsearch, Kafka UI, Kibana). Catalog starts without these.
# connect-register POSTs deploy/debezium/catalog-product-connector.json after Catalog created mt_doc_product.
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait kafka connect elasticsearch kibana kafka-ui connect-register

# Grafana / Loki / OTLP + cluster metrics (optional; Catalog does not require these)
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait \
  catalogdb postgres-exporter cadvisor elasticsearch elasticsearch-exporter \
  kafka kafka-exporter lgtm

# Loki process logs via Alloy (optional; Catalog and Kafka do not require log-collector)
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait \
  catalogdb postgres-exporter cadvisor lgtm kafka catalogapi log-collector
```

`--wait` returns only when healthchecks pass.

**Stop:** `docker compose ... stop` or `down` without `-v`. `down -v` deletes volume `catalog-pgdata`.

---

## Run on k3s (WSL2)

Official local cluster is **k3s in Ubuntu on WSL2** (containerd). Docker, Podman, minikube, k3d, kind, and Docker Desktop Kubernetes are not required. Compose files stay; running compose is not required.

All commands below are run **inside Ubuntu WSL**, from the repo root (example: `/mnt/c/Khoi/Rider/eShop`). If `kubectl` is not on your PATH, use `sudo k3s kubectl` instead.

### 1. One-time: systemd + k3s + .NET 10

Enable systemd if `systemctl` is missing. In Ubuntu:

```bash
sudo tee /etc/wsl.conf >/dev/null <<'EOF'
[boot]
systemd=true
EOF
```

Then from **Windows**: `wsl --shutdown`. Open Ubuntu again.

Install k3s:

```bash
curl -sfL https://get.k3s.io | INSTALL_K3S_EXEC="--disable traefik --disable servicelb" sh -
```

Install the .NET 10 SDK if `dotnet` is not found:

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet:$HOME/.dotnet/tools' >> ~/.bashrc
source ~/.bashrc
dotnet --version
```

Optional, so you can type `kubectl`:

```bash
mkdir -p ~/.kube
sudo k3s kubectl config view --raw > ~/.kube/config
sudo chmod 644 /etc/rancher/k3s/k3s.yaml
export KUBECONFIG=/etc/rancher/k3s/k3s.yaml
```

### 2. Build and import the Catalog image (no Docker)

`dotnet publish` writes the tar next to the Catalog project, not the repo root.

```bash
cd /mnt/c/Khoi/Rider/eShop

dotnet publish src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj \
  -c Release \
  /t:PublishContainer \
  /p:ContainerRepository=eshop-catalog-api \
  /p:ContainerImageTag=local \
  /p:ContainerArchiveOutputPath=./eshop-catalog-api-local.tar

sudo k3s ctr images import src/Services/eShop.Catalog.Api/eshop-catalog-api-local.tar
# image name: eshop-catalog-api:local
```

First-time import only. After Catalog code changes, use **2b** — the same tag with `imagePullPolicy: Never` does not restart the pod.

### 2b. Redeploy Catalog (code changes)

Publish, import `eshop-catalog-api:local`, then **restart** Catalog. Git Bash treats `/t:` and `/p:` as paths, so this script uses `-t:` and `-p:`. Ubuntu bash accepts both.

Inside Ubuntu:

```bash
cd /mnt/c/Khoi/Rider/eShop

dotnet publish src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj \
  -c Release \
  -t:PublishContainer \
  -p:ContainerRepository=eshop-catalog-api \
  -p:ContainerImageTag=local \
  -p:ContainerArchiveOutputPath=./eshop-catalog-api-local.tar

sudo k3s ctr images import src/Services/eShop.Catalog.Api/eshop-catalog-api-local.tar
sudo k3s kubectl -n catalog rollout restart deploy/catalogapi
sudo k3s kubectl -n catalog rollout status deploy/catalogapi
```

From Windows Git Bash (if `sudo` waits for a password, run k3s as root — no `sudo`):

```bash
cd /c/Khoi/Rider/eShop

dotnet publish src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj \
  -c Release \
  -t:PublishContainer \
  -p:ContainerRepository=eshop-catalog-api \
  -p:ContainerImageTag=local \
  -p:ContainerArchiveOutputPath=./eshop-catalog-api-local.tar

wsl.exe -d Ubuntu -u root -- bash -lc \
  'k3s ctr images import /mnt/c/Khoi/Rider/eShop/src/Services/eShop.Catalog.Api/eshop-catalog-api-local.tar'
wsl.exe -d Ubuntu -u root -- bash -lc \
  'k3s kubectl -n catalog rollout restart deploy/catalogapi && k3s kubectl -n catalog rollout status deploy/catalogapi --timeout=180s'
```

Expect Catalog `1/1`. Then `GET http://<wsl-ip>:30050/products?pageNumber=1&pageSize=10`.

### 3. Deploy all services

`kubectl apply -k deploy/k8s` creates every compose identity. Catalog Ready is store-only (`catalogdb` then `catalogapi`). Wait the rest if you want search and viewer up, not to make Catalog Ready.

| Namespace | Service | Kind |
|-----------|---------|------|
| `catalog` | `catalogdb` | Deployment |
| `catalog` | `catalogapi` | Deployment |
| `search` | `elasticsearch` | Deployment |
| `search` | `kafka` | Deployment |
| `search` | `connect` | Deployment |
| `search` | `connect-register` | Job |
| `search` | `kibana` | Deployment |
| `search` | `kafka-ui` | Deployment |
| `viewer` | `lgtm` | Deployment |
| `viewer` | `log-collector` | Deployment |
| `viewer` | `postgres-exporter` | Deployment |
| `viewer` | `elasticsearch-exporter` | Deployment |
| `viewer` | `kafka-exporter` | Deployment |
| `viewer` | `cadvisor` | Deployment |

Inside Ubuntu:

```bash
cd /mnt/c/Khoi/Rider/eShop
sudo systemctl start k3s
kubectl apply -f deploy/k8s/namespace.yaml
kubectl apply -k deploy/k8s

kubectl -n catalog rollout status deploy/catalogdb
kubectl -n catalog rollout status deploy/catalogapi

kubectl -n search rollout status deploy/elasticsearch
kubectl -n search rollout status deploy/kafka
kubectl -n search rollout status deploy/connect
kubectl -n search rollout status deploy/kibana
kubectl -n search rollout status deploy/kafka-ui
kubectl -n search wait --for=condition=complete job/connect-register --timeout=300s

kubectl -n viewer rollout status deploy/lgtm
kubectl -n viewer rollout status deploy/log-collector
kubectl -n viewer rollout status deploy/postgres-exporter
kubectl -n viewer rollout status deploy/elasticsearch-exporter
kubectl -n viewer rollout status deploy/kafka-exporter
kubectl -n viewer rollout status deploy/cadvisor

sudo k3s kubectl get pods -A
```

From Windows Git Bash (if `sudo` waits for a password, run as root):

```bash
wsl.exe -d Ubuntu -u root -- bash -lc 'cd /mnt/c/Khoi/Rider/eShop
set -e
systemctl start k3s
k3s kubectl apply -f deploy/k8s/namespace.yaml
k3s kubectl apply -k deploy/k8s
k3s kubectl -n catalog rollout status deploy/catalogdb
k3s kubectl -n catalog rollout status deploy/catalogapi
k3s kubectl -n search rollout status deploy/elasticsearch
k3s kubectl -n search rollout status deploy/kafka
k3s kubectl -n search rollout status deploy/connect
k3s kubectl -n search rollout status deploy/kibana
k3s kubectl -n search rollout status deploy/kafka-ui
k3s kubectl -n search wait --for=condition=complete job/connect-register --timeout=300s
k3s kubectl -n viewer rollout status deploy/lgtm
k3s kubectl -n viewer rollout status deploy/log-collector
k3s kubectl -n viewer rollout status deploy/postgres-exporter
k3s kubectl -n viewer rollout status deploy/elasticsearch-exporter
k3s kubectl -n viewer rollout status deploy/kafka-exporter
k3s kubectl -n viewer rollout status deploy/cadvisor
k3s kubectl get pods -A
'
```

Official apply hosts namespaces `catalog`, `search`, and `viewer`. Catalog HTTP works after the two catalog rollouts even if search or viewer is still starting.

### 3a. Redeploy all services (YAML changes)

Does **not** rebuild Catalog code (use **2b** for that). Apply manifests, restart every Deployment, recreate the register Job:

```bash
cd /mnt/c/Khoi/Rider/eShop
kubectl apply -f deploy/k8s/namespace.yaml
kubectl apply -k deploy/k8s

kubectl -n catalog rollout restart deploy/catalogdb
kubectl -n catalog rollout restart deploy/catalogapi
kubectl -n search rollout restart deploy/elasticsearch
kubectl -n search rollout restart deploy/kafka
kubectl -n search rollout restart deploy/connect
kubectl -n search rollout restart deploy/kibana
kubectl -n search rollout restart deploy/kafka-ui
kubectl -n viewer rollout restart deploy/lgtm
kubectl -n viewer rollout restart deploy/log-collector
kubectl -n viewer rollout restart deploy/postgres-exporter
kubectl -n viewer rollout restart deploy/elasticsearch-exporter
kubectl -n viewer rollout restart deploy/kafka-exporter
kubectl -n viewer rollout restart deploy/cadvisor

kubectl -n search delete job connect-register --ignore-not-found
kubectl apply -k deploy/k8s

kubectl -n catalog rollout status deploy/catalogdb
kubectl -n catalog rollout status deploy/catalogapi
kubectl -n search rollout status deploy/elasticsearch
kubectl -n search rollout status deploy/kafka
kubectl -n search rollout status deploy/connect
kubectl -n search rollout status deploy/kibana
kubectl -n search rollout status deploy/kafka-ui
kubectl -n search wait --for=condition=complete job/connect-register --timeout=300s
kubectl -n viewer rollout status deploy/lgtm
kubectl -n viewer rollout status deploy/log-collector
kubectl -n viewer rollout status deploy/postgres-exporter
kubectl -n viewer rollout status deploy/elasticsearch-exporter
kubectl -n viewer rollout status deploy/kafka-exporter
kubectl -n viewer rollout status deploy/cadvisor
```

From Windows Git Bash:

```bash
wsl.exe -d Ubuntu -u root -- bash -lc 'cd /mnt/c/Khoi/Rider/eShop
set -e
k3s kubectl apply -f deploy/k8s/namespace.yaml
k3s kubectl apply -k deploy/k8s
k3s kubectl -n catalog rollout restart deploy/catalogdb
k3s kubectl -n catalog rollout restart deploy/catalogapi
k3s kubectl -n search rollout restart deploy/elasticsearch
k3s kubectl -n search rollout restart deploy/kafka
k3s kubectl -n search rollout restart deploy/connect
k3s kubectl -n search rollout restart deploy/kibana
k3s kubectl -n search rollout restart deploy/kafka-ui
k3s kubectl -n viewer rollout restart deploy/lgtm
k3s kubectl -n viewer rollout restart deploy/log-collector
k3s kubectl -n viewer rollout restart deploy/postgres-exporter
k3s kubectl -n viewer rollout restart deploy/elasticsearch-exporter
k3s kubectl -n viewer rollout restart deploy/kafka-exporter
k3s kubectl -n viewer rollout restart deploy/cadvisor
k3s kubectl -n search delete job connect-register --ignore-not-found
k3s kubectl apply -k deploy/k8s
k3s kubectl get pods -A
'
```

### 3b. Check Kubernetes

All in Ubuntu WSL. If `kubectl` is on PATH, drop `sudo k3s`. Catalog Ready is store-only; search and viewer can still be starting.

Cluster and pods:

```bash
sudo systemctl is-active k3s
sudo k3s kubectl get pods -A
sudo k3s kubectl get pods,svc -n catalog
sudo k3s kubectl get pods,svc -n search
sudo k3s kubectl get pods,svc -n viewer
```

Expect catalog `catalogdb` and `catalogapi` `1/1`. Search/viewer may still be `0/1` or `Init`.

Rollouts:

```bash
sudo k3s kubectl -n catalog rollout status deploy/catalogdb
sudo k3s kubectl -n catalog rollout status deploy/catalogapi
sudo k3s kubectl -n search rollout status deploy/elasticsearch
sudo k3s kubectl -n search rollout status deploy/kafka
sudo k3s kubectl -n search rollout status deploy/connect
sudo k3s kubectl -n search rollout status deploy/kibana
sudo k3s kubectl -n search rollout status deploy/kafka-ui
sudo k3s kubectl -n viewer rollout status deploy/lgtm
sudo k3s kubectl -n viewer rollout status deploy/log-collector
sudo k3s kubectl -n viewer rollout status deploy/postgres-exporter
sudo k3s kubectl -n viewer rollout status deploy/elasticsearch-exporter
sudo k3s kubectl -n viewer rollout status deploy/kafka-exporter
sudo k3s kubectl -n viewer rollout status deploy/cadvisor
```

Services, NodePorts, PVC, image:

```bash
sudo k3s kubectl -n catalog get svc catalogapi catalogdb
sudo k3s kubectl -n catalog get pvc catalogdb-data
sudo k3s ctr images ls | grep eshop-catalog
```

Expect `8080:30050/TCP`, `5432:30432/TCP`, PVC `Bound`, image `eshop-catalog-api:local`. `EXTERNAL-IP` stays `<none>`.

Not Ready, events, logs:

```bash
sudo k3s kubectl get events -A --sort-by=.lastTimestamp | tail -30
sudo k3s kubectl -n catalog describe pod -l app=catalogapi
sudo k3s kubectl -n catalog logs -l app=catalogapi --tail=50
sudo k3s kubectl -n search logs job/connect-register --tail=50
sudo k3s kubectl -n search describe pod -l app=connect
```

Catalog store (Postgres) and search index (Elasticsearch):

```bash
sudo k3s kubectl -n catalog exec deploy/catalogdb -- \
  psql -U catalog -d catalog -c "\dt"
sudo k3s kubectl -n catalog exec deploy/catalogdb -- \
  psql -U catalog -d catalog -c "SELECT count(*) FROM mt_doc_product;"
sudo k3s kubectl -n search exec deploy/elasticsearch -- \
  curl -sS "http://localhost:9200/_cat/indices?v"
```

Empty PVC: no `mt_doc_product` until Catalog starts (or first write). Browse reads Elasticsearch `catalog-products`, not Postgres.

Catalog HTTP (WSL IP + NodePort, no port-forward):

```bash
hostname -I | awk '{print $1}'
curl -sS "http://<wsl-ip>:30050/products?pageNumber=1&pageSize=10"
```

### 4. Connect (port-forward)

NodePort cannot be `5000` (Kubernetes NodePort range is 30000–32767), so the host URL is port-forward. Leave this terminal open:

```bash
bash deploy/k8s/port-forward-localhost.sh
# uses sudo k3s kubectl if kubectl is not on PATH
```

Or Catalog only: `kubectl -n catalog port-forward svc/catalogapi 5000:8080`.

Then open these on Windows (the script binds `--address 0.0.0.0`):

| Service | URL |
|---------|-----|
| Catalog / Swagger | http://localhost:5000 |
| Grafana (lgtm) | http://localhost:3000 (admin / admin) |
| Grafana Ops/Infra | http://localhost:3000/d/eshop-ops-infra/ops-infra |
| Kafka UI | http://localhost:8080 |
| Kibana | http://localhost:5601 |
| Elasticsearch | http://localhost:9200 |
| Debezium Connect | http://localhost:8083 |
| Postgres | `localhost:5432` user/db `catalog` / `catalog` |

`deploy/k8s/port-forward-localhost.sh` maps those host ports. Ctrl+C stops every forward.

Catalog create / get-by-id work as soon as `catalogapi` is `1/1`, even if Kafka or Grafana is not Ready.

After `lgtm` and the metric exporters have been Ready about one minute (search may still be starting), open Ops/Infra (`http://localhost:3000/d/eshop-ops-infra/ops-infra`, admin / admin). Store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, and exporters must appear: Ready = healthy with samples; not-ready or stopped = not healthy. Empty panels are a defect. Collector and Grafana need not appear. If Windows `localhost` fails, use `127.0.0.1`. Create and get-by-id still succeed if that metrics path is down.

### 4b. Connect without the script (NodePort)

Grafana, Kafka UI, Kibana, Catalog, and Postgres publish NodePorts. Those stay reachable after you close the port-forward script. Kubernetes NodePort cannot be `5000` or `5432`, so Catalog is `:30050` and Postgres is `:30432`.

**`EXTERNAL-IP` stays `<none>`.** That column is for LoadBalancer. This cluster installed k3s with `--disable servicelb`, so nothing assigns an External-IP. ClusterIP never gets one either. That is expected. You do **not** wait for an External-IP.

Access is **WSL IP + NodePort** (same idea as Grafana `:30000`).

Apply if you have not already, then read the NodePort column:

```bash
sudo k3s kubectl apply -k deploy/k8s
sudo k3s kubectl -n catalog get svc catalogapi catalogdb
```

Expect `8080:30050/TCP` and `5432:30432/TCP`. `EXTERNAL-IP` is still `<none>`.

In WSL, get the Ubuntu address:

```bash
hostname -I | awk '{print $1}'
```

On Windows, open that IP (not `localhost`):

| Service | URL |
|---------|-----|
| Grafana | `http://<wsl-ip>:30000` (admin / admin) |
| Grafana Ops/Infra | `http://<wsl-ip>:30000/d/eshop-ops-infra/ops-infra` |
| Kafka UI | `http://<wsl-ip>:30080` |
| Kibana | `http://<wsl-ip>:30561` |
| Catalog | `http://<wsl-ip>:30050` |
| Postgres | Host `<wsl-ip>`, port `30432`, db/user/password `catalog` |

`localhost:5000` still needs the port-forward script. Optional Catalog-only tunnel (leave it running):

```bash
sudo k3s kubectl -n catalog port-forward svc/catalogapi 5000:8080 --address 0.0.0.0
```

### 4c. Browse empty while Postgres has products

First time (empty PVC): Postgres **auto create**s database `catalog` (`POSTGRES_DB`). Catalog then applies Marten schema (`public.mt_doc_product`) because Kafka is set. **Then** Debezium Connect starts and `connect-register` POSTs the connector.

`GET /products` and `GET /products/{id}` both read Elasticsearch `catalog-products`, not `mt_doc_product`. If Debezium registered **before** Marten created that table, the publication is empty and **no Kafka events fire**. Browse stays empty and get-by-id is 404 until sync runs; Postgres may still have the rows.

Re-snapshot the 46 (or any) existing rows — in WSL:

```bash
# Connect API (script or one-shot)
sudo k3s kubectl -n search port-forward svc/connect 8083:8083 --address 0.0.0.0
```

Other terminal:

```bash
curl -sS http://127.0.0.1:8083/connectors/eshop-catalog-product/status
curl -sS -X DELETE http://127.0.0.1:8083/connectors/eshop-catalog-product

sudo k3s kubectl -n catalog exec deploy/catalogdb -- \
  psql -U catalog -d catalog -c "SELECT slot_name, active FROM pg_replication_slots;"
sudo k3s kubectl -n catalog exec deploy/catalogdb -- \
  psql -U catalog -d catalog -c "SELECT pubname FROM pg_publication;"
sudo k3s kubectl -n catalog exec deploy/catalogdb -- \
  psql -U catalog -d catalog -c "SELECT * FROM pg_publication_tables;"

sudo k3s kubectl -n catalog exec deploy/catalogdb -- \
  psql -U catalog -d catalog -c "SELECT pg_drop_replication_slot('eshop_catalog_sync');"
sudo k3s kubectl -n catalog exec deploy/catalogdb -- \
  psql -U catalog -d catalog -c "DROP PUBLICATION IF EXISTS dbz_publication;"

sudo k3s kubectl -n search delete job connect-register
sudo k3s kubectl apply -k deploy/k8s
sudo k3s kubectl -n search logs job/connect-register --follow
```

Expect connector `RUNNING`, then:

```bash
curl -sS http://<wsl-ip>:30050/products?pageNumber=1\&pageSize=10
```

`connect-register` now waits until `public.mt_doc_product` exists before it POSTs the connector.

### 5. Stop

**Ordinary stop:** Ctrl+C the port-forward, then `sudo systemctl stop k3s`. PVC `catalogdb-data` stays. Compose files stay.

**Destructive stop** (empty cluster store): `kubectl delete pvc catalogdb-data -n catalog`. Do not delete `deploy/compose.yaml`.

Isolated `dotnet test` does not start k3s.

---

## Endpoints

### Catalog.Api (`catalogapi`)

Host **5000** → container **8080**. HTTP only. Swagger is on in Development.

| What | Method | Path | Reads |
|------|--------|------|-------|
| Create product | `POST` | `/products` | catalog store (write) |
| Page / search products | `GET` | `/products` | Elasticsearch `catalog-products` |
| Import products from Excel | `POST` | `/products/import` | catalog store (write) |
| Get product by id | `GET` | `/products/{id}` | Elasticsearch `catalog-products` |
| Update product | `PUT` | `/products/{id}` | catalog store (write) |
| OpenAPI document | `GET` | `/openapi/v1.json` |
| Swagger UI | `GET` | `/swagger` |

Auth: none. No public delete or activate routes.

`GET /products` query parameters:

| Name | Default | Rules |
|------|---------|-------|
| `pageNumber` | `1` | Must be ≥ 1 |
| `pageSize` | `10` | Must be 1–100 |
| `orderBy` | `CreatedAt` | `CreatedAt`, `LastModified`, `Name`, `CostPrice`, `SewingCost`, `FabricCostAccessoriesCost`, `PackagingCost`, or `Amount` |
| `orderDirection` | `desc` | `asc` or `desc` |
| `name` | omitted | Case-insensitive contains on the **whole name** (not tokenized words). Vietnamese full names work, e.g. `Đầm Alice - size 1 (Xanh dương)`. Accent-folding is not applied: `dam` does not match `Đầm`. |
| `costPrice`, `sewingCost`, `fabricCostAccessoriesCost`, `packagingCost` | omitted | Exact match; reject `< 0` |
| `category` | omitted | Case-insensitive exact match against one listed category |

Combined search fields are AND. Blank `name` or `category` is ignored. Sort ties are ordered by `id` ascending. Only active, not-deleted products are returned. `GET /products` JSON includes `orderBy` and `orderDirection` as **string** enum names (`CreatedAt`, `Desc`, not `0` / `1`). Query parameters use the same names (`?orderBy=Name&orderDirection=Asc`). `GET /products` and `GET /products/{id}` read Elasticsearch, not the catalog store. A product that exists only in Postgres is omitted from browse and is **404** on get-by-id until search sync indexes it. Index down → `503` `{ message }` (no stack) on browse and get-by-id. Missing index (nothing synced yet) → browse `200` with `items: []`; get-by-id `404`.

Operator UIs (optional, not required for `dotnet test`):

| UI | URL | Use |
|----|-----|-----|
| Kafka UI | http://localhost:8080 | Filter topics `eshop.catalog.sync` |
| Kibana | http://localhost:5601 | Data view `catalog-products` |
| Grafana Ops/Infra | http://localhost:3000 | Product-stack healthy/not; click for CPU, RAM, and richer detail (admin/admin) |
| Grafana Log/App | http://localhost:3000 | Elasticsearch `catalog-app-logs*`: Catalog Error/Fatal |
| Exporter metrics | http://localhost:9114/metrics | `elasticsearch_cluster_health_` series when ES is up |
| Kafka exporter | http://localhost:9308/metrics | `kafka_brokers` / topic / lag when Kafka is up |

In-stack: `catalogapi:8080`. Published host port: `5000`.

```http
POST /products
Content-Type: application/json

{
  "name": "Trail Bike",
  "amount": 5,
  "costPrice": 1299.50,
  "sewingCost": 0,
  "fabricCostAccessoriesCost": 0,
  "packagingCost": 0,
  "description": "A sturdy trail bike",
  "imageFile": "trail-bike.png",
  "categories": ["bikes", "outdoor"]
}
```

Required JSON: `name`, `amount`, `costPrice`, `sewingCost`, `fabricCostAccessoriesCost`, `packagingCost`, `description`, `imageFile`, `categories`. Success: `201` and a non-empty `id`. `Location: /products/{id}`.

Import products with `POST /products/import` using `multipart/form-data` with a file field named `file`. Upload the `.xlsx` workbook; the importer reads product rows from the `CHI PHÍ` sheet. It maps `Tên sản phẩm`, `Số lượng đặt may`, `Chi phí may/SP`, `Vải + Phụ liệu/SP`, `Đóng gói/SP`, and `Giá vốn/SP` to product fields. A blank cost is treated as zero; if per-product cost is blank, it is calculated from the three component costs. Rows without a product name are skipped. The workbook does not supply description, image, or category values, so those are empty on imported products.

Import (like create) returns after the catalog-store commit. Those products appear on `GET /products` and `GET /products/{id}` only after search sync: start `connect-register` with the search stack, then wait a few seconds. If search sync is not running, import still succeeds; browse stays empty or returns 503 if Elasticsearch is down, and get-by-id is 404 until the document is indexed.

```powershell
curl.exe -X POST http://localhost:5000/products/import -F "file=@products.xlsx"
```

```http
GET /products?pageNumber=1&pageSize=10&name=bike&category=outdoor
GET /products?name=Đầm Alice - size 1 (Xanh dương)
```

Success: `200` with `pageNumber`, `pageSize`, `totalCount`, and `items`.

Check that Debezium is registered after search sync is up:

```bash
curl http://localhost:8083/connectors/eshop-catalog-product/status
```

### PostgreSQL (`catalogdb`)

`postgres:16.15`. Local-development credentials only. TLS is off.

| | In-stack (`catalogapi`) | Published port |
|--|-------------------------|----------------|
| Host | `catalogdb` | Docker Desktop engine |
| Port | `5432` | `5432` |
| Database / user / password | `catalog` | `catalog` |

In-stack Npgsql:

```
Host=catalogdb;Port=5432;Database=catalog;Username=catalog;Password=catalog;SSL Mode=Disable
```

```bash
docker exec -it eshop_catalog_db psql -U catalog -d catalog
```

### Grafana LGTM (`lgtm`)

One container. Sign-in: **admin** / **admin**. Catalog still starts if LGTM is down.

| What | In-stack | Published port |
|------|----------|----------------|
| Grafana UI | `lgtm:3000` | `3000` |
| OTLP HTTP | `lgtm:4318` | `4318` |
| OTLP gRPC | `lgtm:4317` | `4317` |

Catalog compose sends OTLP to `http://lgtm:4318`. Catalog Error/Fatal logs also go to Elasticsearch `catalog-app-logs` when `Serilog__Elasticsearch__Nodes__0` is set. Isolated `dotnet test` does not set that sink.

Loki (`3100`), Tempo (`3200`), Prometheus (`9090`) stay inside the container. In Grafana:

- **Ops/Infra** (Prometheus only) — one product-stack overview (Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, exporters). Healthy = process up. Stopped or missing = not healthy. Overview shows **CPU and RAM** for those identities. **Catalog Db** is its own row (up, connections, size). Zero store size or ES documents is valid and is not not-healthy. A bottom **Grafana / LGTM disk** panel shows Prometheus TSDB size inside `lgtm`. There is no service dropdown. Collector and Grafana are not required on the health overview. Catalog does not scrape the store or cadvisor.
- **Log/App** (Elasticsearch data source, provisioned) — why it broke? Filter `service.name` = `eShop.Catalog.Api`.
- **Explore → Loki** — process-line search. Loki is not replaced. Do not add a Loki data source. Last **15 minutes**. Empty before first write is valid.

```bash
curl -s http://localhost:9114/metrics | findstr elasticsearch_cluster_health_
curl -s http://localhost:9308/metrics | findstr kafka_brokers
curl -s http://localhost:9187/metrics | findstr pg_up
```

Grafana: compose `http://127.0.0.1:3000` or k3s NodePort `http://<wsl-ip>:30000` → **Explore → Loki**. Same LogQL on both (k3s Alloy sets `compose_service` from the pod `app` label).

**Catalog application** (OTLP only — not a container copy):

```logql
{service_name=~".*eShop.Catalog.Api.*"}
{service_name=~".*eShop.Catalog.Api.*"} |= "Error"
{service_name=~".*eShop.Catalog.Api.*"} |= "failed"
```

**trace_id / span_id** (on Catalog **request** rows only; startup lines have none). Console text uses `TraceId:` / `SpanId:`. OTLP fields are `trace_id` / `span_id`. Paste an id from a request row or from Tempo.

```logql
{service_name=~".*eShop.Catalog.Api.*"} |= "TraceId:"
{service_name=~".*eShop.Catalog.Api.*"} | line_format "{{__line__}}\ntrace_id='{{.trace_id}}'\nspan_id='{{.span_id}}'"
{service_name=~".*eShop.Catalog.Api.*"} | trace_id!="" | line_format "{{.trace_id}}  {{.span_id}}  {{__line__}}"
{service_name=~".*eShop.Catalog.Api.*"} | trace_id="<paste-trace-id>"
{service_name=~".*eShop.Catalog.Api.*"} | span_id="<paste-span-id>"
{service_name=~".*eShop.Catalog.Api.*"} |= "<paste-trace-id>"
```

**Store / search-sync process lines** (Alloy → Loki). `{compose_service="kafka"}` is broker process text, not topic JSON — payloads stay in Kafka UI.

```logql
{compose_service="catalogdb"}
{compose_service="kafka"}
{compose_service="connect"}
{compose_service="elasticsearch"}
{compose_service="kibana"}
{compose_service="kafka-ui"}
```

**Exporters / cadvisor** (when those pods are up):

```logql
{compose_service="postgres-exporter"}
{compose_service="elasticsearch-exporter"}
{compose_service="kafka-exporter"}
{compose_service="cadvisor"}
```

Useful filters on any of the above:

```logql
{compose_service="catalogdb"} |= "error" or |= "ERROR" or |= "fatal"
{compose_service="elasticsearch"} |= "Exception"
{service_name=~".*eShop.Catalog.Api.*"} | json | level="Error"
```

Do **not** use a Catalog container-tail selector for request logs. Catalog request lines use the `service_name` queries only. Catalog and Kafka stay up if `log-collector`, `cadvisor`, or `postgres-exporter` is stopped.

`trace_id` / `span_id` are fields on **request** rows, not in the default message text. Startup lines have none. After a request, copy `trace_id` from Loki and paste it in **Explore → Tempo** (service `eShop.Catalog.Api`) to open the same waterfall.

After Catalog handles at least one public request with LGTM up, open Grafana **Explore → Tempo → Search** and filter service name `eShop.Catalog.Api`. That request’s waterfall must appear. A Tempo service graph is not required. Catalog still starts if Tempo is down.

---

## Tests

Isolated: no compose, no live Postgres, no Grafana, no Elasticsearch exporter. Catalog tests use a dummy `CatalogDb` and an in-memory document session.

```bash
dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj
dotnet test tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj
dotnet test tests/BuildingBlocks/BuildBlock.Logging.Tests/BuildBlock.Logging.Tests.csproj
```

---

## Specs

| Spec | What it added |
|------|----------------|
| [001-serilog-otel-logging](specs/001-serilog-otel-logging/spec.md) | Shared Serilog + OTLP, TraceId/SpanId, fail-open export |
| [002-activate-loki-grafana](specs/002-activate-loki-grafana/spec.md) | LGTM in compose; Loki already selected in Grafana |
| [003-catalog-buildingblocks-tests](specs/003-catalog-buildingblocks-tests/spec.md) | Isolated Catalog + building-block tests |
| [004-catalog-postgres-compose](specs/004-catalog-postgres-compose/spec.md) | `catalogdb` + `catalogapi` in compose |
| [005-catalog-page-entities](specs/005-catalog-page-entities/spec.md) | Paginated browse, name/price/category search, shared `BaseEntity` |
| [006-hybrid-observability](specs/006-hybrid-observability/spec.md) | Grafana Ops/Infra (Prometheus + exporter) and Log/App (Catalog Error → ES); fail-open Catalog |
| [007-loki-promtail](specs/007-loki-promtail/spec.md) | Alloy collector: Catalog + Kafka broker stdout → existing Loki; topic payloads stay in Kafka UI |
| [008-all-container-logs](specs/008-all-container-logs/spec.md) | All non-app container logs in Loki (no Catalog duplicate); Ops/Infra overview + click for CPU/RAM and store health |
| [009-local-k8s-wsl2](specs/009-local-k8s-wsl2/spec.md) | k3s in Ubuntu WSL2 hosts the full compose-equivalent stack; Catalog URL stays `localhost:5000` via port-forward |

New production C# should follow an existing spec (or a new one) and TDD.
