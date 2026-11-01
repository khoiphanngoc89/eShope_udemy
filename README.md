# eShop

Local .NET e-commerce backend. Today it is one vertical: **Catalog** (create, update, get, and page/search products), shared building blocks, PostgreSQL, and optional Grafana/Loki for logs.

Work is spec-driven (`specs/`). Catalog’s documented run path is **compose**, not `dotnet run`. Isolated tests do not need Postgres or Grafana.

**Stack:** .NET 10, Carter, MediatR, Marten, Serilog → OTLP, PostgreSQL 16.15, Grafana LGTM 0.38.0.

Solution: `eShop.slnx`.

---

## What it does

A client `POST`s a product to Catalog.Api. Catalog stores it in PostgreSQL through Marten and returns `201` with a new `id`. Clients can `GET /products` (page plus optional name, price, or category search **from Elasticsearch**), `GET /products/{id}` (catalog store), and `PUT /products/{id}`. If the search index is down, `GET /products` returns **503**; create and get-by-id still work. Debezium copies store changes onto Kafka topics prefixed `eshop.catalog.sync`. Request logs go to the console and, when LGTM is up, to Loki. Correlation IDs (`trace_id`, `span_id`) are attached when ASP.NET has a request activity. There is no identity, basket, or ordering service yet.

```
Client  →  catalogapi :8080 (published :5000)  →  catalogdb :5432 (Marten)
                      │                            │
                      │                            └── Debezium → kafka → indexer → elasticsearch
                      ├── kafka-ui :8080  (filter eshop.catalog.sync)
                      ├── kibana :5601    (index catalog-products)
                      └── lgtm :4318 (Grafana :3000, Loki, Tempo)
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
specs/                      Feature specs, plans, contracts
```

| Project | Role |
|---------|------|
| `BuildingBlock` | MediatR command/query markers; Carter module registration |
| `BuildBlock.Logging` | Shared logging. Each service owns `Serilog` in its own `appsettings.json` |
| `eShop.Catalog.Api` | Public Catalog HTTP API |

Catalog wiring: `Program.cs` → `AddCatalogServices()` / `UsePipline()`. Endpoints are Carter modules (for example `CreateProductEndpoint`). Persistence is Marten lightweight sessions; schema is `AutoCreate.CreateOrUpdate` on first write. Required setting: `ConnectionStrings:CatalogDb`.

---

## Run locally

Docker Desktop. From the repo root:

```bash
# Store only
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait catalogdb

# Catalog.Api (starts catalogdb; Grafana not required)
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait catalogapi

# Search sync (Kafka, Debezium, Elasticsearch, Kafka UI, Kibana). Catalog starts without these.
# connect-register snapshots mt_doc_product after Catalog has created the table.
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait kafka connect elasticsearch kibana kafka-ui connect-register

# Grafana / Loki / OTLP (optional)
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait lgtm
```

`--wait` returns only when healthchecks pass.

**Stop:** `docker compose ... stop` or `down` without `-v`. `down -v` deletes volume `catalog-pgdata`.

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
| `name` | omitted | Case-insensitive contains |
| `costPrice`, `sewingCost`, `fabricCostAccessoriesCost`, `packagingCost` | omitted | Exact match; reject `< 0` |
| `category` | omitted | Case-insensitive exact match against one listed category |

Combined search fields are AND. Blank `name` or `category` is ignored. Sort ties are ordered by `id` ascending. Only active, not-deleted products are returned. `GET /products` reads Elasticsearch, not the catalog store. Index down → `503` `{ message }` (no stack).

Operator UIs (optional, not required for `dotnet test`):

| UI | URL | Use |
|----|-----|-----|
| Kafka UI | http://localhost:8080 | Filter topics `eshop.catalog.sync` |
| Kibana | http://localhost:5601 | Data view `catalog-products` |

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

```powershell
curl.exe -X POST http://localhost:5000/products/import -F "file=@products.xlsx"
```

```http
GET /products?pageNumber=1&pageSize=10&name=bike&category=outdoor
```

Success: `200` with `pageNumber`, `pageSize`, `totalCount`, and `items`.

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

Catalog compose sends OTLP to `http://lgtm:4318`.

Loki (`3100`), Tempo (`3200`), Prometheus (`9090`) stay inside the container. Query logs in Grafana: **Explore → Loki**.

```logql
{service_name=~".*eShop.Catalog.Api.*"}
| line_format "{{__line__}}\ntrace_id='{{.trace_id}}'\nspan_id='{{.span_id}}'"
```

`trace_id` / `span_id` are fields on **request** rows, not in the default message text. Startup lines have none.

After Catalog handles at least one public request with LGTM up, open Grafana **Explore → Tempo → Search** and filter service name `eShop.Catalog.Api`. That request’s waterfall must appear. A Tempo service graph is not required. Catalog still starts if Tempo is down.

---

## Tests

Isolated: no compose, no live Postgres, no Grafana. Catalog tests use a dummy `CatalogDb` and an in-memory document session.

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

New production C# should follow an existing spec (or a new one) and TDD.
