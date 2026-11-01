# eShop

Local .NET e-commerce backend. Today it is one vertical: **Catalog** (create, update, get, and page/search products), shared building blocks, PostgreSQL, and optional Grafana (Ops/Infra + Log/App) plus Loki.

Work is spec-driven (`specs/`). Catalog’s documented run path is **compose**, not `dotnet run`. Isolated tests do not need Postgres, Grafana, or the Elasticsearch exporter.

**Stack:** .NET 10, Carter, MediatR, Marten, Serilog → OTLP, PostgreSQL 16.15, Grafana LGTM 0.38.0, elasticsearch-exporter v1.8.0.

Solution: `eShop.slnx`.

---

## What it does

A client `POST`s a product to Catalog.Api. Catalog stores it in PostgreSQL through Marten and returns `201` with a new `id`. Clients can `GET /products` (page plus optional name, price, or category search **from Elasticsearch**), `GET /products/{id}` (catalog store), `PUT /products/{id}`, and `POST /products/import`. Create, update, and import do **not** write Elasticsearch. Debezium snapshots then streams `mt_doc_product` onto Kafka topics prefixed `eshop.catalog.sync`; Catalog’s indexer upserts `catalog-products`. If Elasticsearch is down, `GET /products` returns **503**; create, import, and get-by-id still work. If the index does not exist yet (no document has been synced), `GET /products` is **200** with empty `items`, not 503. Request logs go to the console and, when LGTM is up, to Loki. Compose also dual-writes Catalog **Error/Fatal** logs to Elasticsearch index `catalog-app-logs` (fail-open). Correlation IDs (`trace_id`, `span_id`) are attached when ASP.NET has a request activity. There is no identity, basket, or ordering service yet.

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

Redo this step only after Catalog code changes.

### 3. Start the stack

```bash
sudo systemctl start k3s
kubectl apply -f deploy/k8s/namespace.yaml
kubectl apply -k deploy/k8s
kubectl -n catalog rollout status deploy/catalogdb
kubectl -n catalog rollout status deploy/catalogapi
```

Official apply hosts namespaces `catalog`, `search`, and `viewer`. Catalog Ready waits only on the store. Search and viewer can still be starting.

Check pods:

```bash
sudo k3s kubectl get pods -A
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

### 5. Stop

**Ordinary stop:** Ctrl+C the port-forward, then `sudo systemctl stop k3s`. PVC `catalogdb-data` stays. Compose files stay.

**Destructive stop** (empty cluster store): `kubectl delete pvc catalogdb-data -n catalog`. Do not delete `deploy/compose.yaml`.

Isolated `dotnet test` does not start k3s.

---

## Endpoints

### Catalog.Api (`catalogapi`)

Host **5000** → container **8080**. HTTP only. Swagger is on in Development.

| What | Method | Path |
|------|--------|------|
| Create product | `POST` | `/products` |
| Page / search products | `GET` | `/products` |
| Import products from Excel | `POST` | `/products/import` |
| Get product by id | `GET` | `/products/{id}` |
| Update product | `PUT` | `/products/{id}` |
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

Combined search fields are AND. Blank `name` or `category` is ignored. Sort ties are ordered by `id` ascending. Only active, not-deleted products are returned. `GET /products` reads Elasticsearch, not the catalog store. Index down → `503` `{ message }` (no stack). Missing index (nothing synced yet) → `200` with `items: []`.

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

Import (like create) returns after the catalog-store commit. Those products appear on `GET /products` only after search sync: start `connect-register` with the search stack, then wait a few seconds. If search sync is not running, import still succeeds and `GET /products/{id}` still works; `GET /products` stays empty or returns 503 if Elasticsearch is down.

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
- **Explore → Loki** — process-line search. Loki is not replaced.

```bash
curl -s http://localhost:9114/metrics | findstr elasticsearch_cluster_health_
curl -s http://localhost:9308/metrics | findstr kafka_brokers
curl -s http://localhost:9187/metrics | findstr pg_up
```

```logql
{service_name=~".*eShop.Catalog.Api.*"}
| line_format "{{__line__}}\ntrace_id='{{.trace_id}}'\nspan_id='{{.span_id}}'"
```

Catalog application lines (OTLP only — not a second container copy). Last 15 minutes. Do not add a Loki data source.

```logql
{service_name=~".*eShop.Catalog.Api.*"}
{service_name=~".*eShop.Catalog.Api.*"} |= "Error"
{service_name=~".*eShop.Catalog.Api.*"} |= "failed"
```

Non-application process lines (Docker stdout → Alloy → Loki). Empty before first write is valid.

```logql
{compose_service="catalogdb"}
{compose_service="kafka"}
{compose_service="connect"}
{compose_service="elasticsearch"}
{compose_service="kibana"}
{compose_service="kafka-ui"}
```

`{compose_service="kafka"}` is Kafka **broker process** lines, not topic payloads — those stay in Kafka UI http://localhost:8080. Catalog request logs use the OTLP selector above, not a second container-tail copy of Catalog.Api. Catalog and Kafka stay up if `log-collector`, `cadvisor`, or `postgres-exporter` is stopped.

`trace_id` / `span_id` are fields on **request** rows, not in the default message text. Startup lines have none.

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
