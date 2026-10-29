# eShop

Local .NET e-commerce backend. Today it is one vertical: **Catalog** (create a product), shared building blocks, PostgreSQL, and optional Grafana/Loki for logs.

Work is spec-driven (`specs/`). Catalog’s documented run path is **compose**, not `dotnet run`. Isolated tests do not need Postgres or Grafana.

**Stack:** .NET 10, Carter, MediatR, Marten, Serilog → OTLP, PostgreSQL 16.15, Grafana LGTM 0.38.0.

Solution: `eShop.slnx`.

---

## What it does

A client `POST`s a product to Catalog.Api. Catalog stores it in PostgreSQL through Marten and returns `201` with a new `id`. Request logs go to the console and, when LGTM is up, to Loki (query them in Grafana). Correlation IDs (`trace_id`, `span_id`) are attached when ASP.NET has a request activity. There is no identity, basket, or ordering service yet.

```
Client  →  http://172.27.104.244:5000  →  PostgreSQL :5432 (Marten)
                      │
                      └── OTLP :4318 → LGTM (Grafana :3000, Loki, Tempo)
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
    eShop.Catalog.Api/      HTTP API, Marten, create-product
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

From the repo root, Podman or Docker:

```bash
# Store only
podman compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait catalogdb

# Catalog.Api (starts catalogdb; Grafana not required)
podman compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait catalogapi

# Grafana / Loki / OTLP (optional)
podman compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait lgtm
```

Same commands work with `docker compose`. `--wait` returns only when healthchecks pass.

**Stop:** `compose stop` or `down` without `-v`. `down -v` deletes volume `catalog-pgdata`.

Published host base: **http://172.27.104.244/** (Podman WSL IP; Windows `localhost` does not bind). If that IP stops working:

```bash
wsl -d podman-machine-default -- ip -4 -o addr show eth0
```

---

## Endpoints

### Catalog.Api (`catalogapi`)

Host **5000** → container **8080**. HTTP only. Swagger is on in Development.

| What | Method | URL |
|------|--------|-----|
| Create product | `POST` | http://172.27.104.244:5000/products |
| OpenAPI document | `GET` | http://172.27.104.244:5000/openapi/v1.json |
| Swagger UI | `GET` | http://172.27.104.244:5000/swagger |

No `GET` / `PUT` / `DELETE` `/products` yet. `201` sets `Location: /products/{id}` but that path is not served. Auth: none.

In-stack: `http://catalogapi:8080`

```http
POST http://172.27.104.244:5000/products
Content-Type: application/json

{
  "name": "Trail Bike",
  "price": 1299.50,
  "description": "A sturdy trail bike",
  "imageFile": "trail-bike.png",
  "categories": ["bikes", "outdoor"]
}
```

Required JSON: `name`, `price`, `description`, `imageFile`, `categories`. Success: `201` and a non-empty `id`.

### PostgreSQL (`catalogdb`)

`postgres:16.15`, host **5432**. Local-development credentials only. TLS is off.

| | Host (Rider / Windows) | In-stack (`catalogapi`) |
|--|------------------------|-------------------------|
| Host | `172.27.104.244` | `catalogdb` |
| Port | `5432` | `5432` |
| Database / user / password | `catalog` | `catalog` |

Npgsql:

```
Host=172.27.104.244;Port=5432;Database=catalog;Username=catalog;Password=catalog;SSL Mode=Disable
```

Rider / JDBC (`SSL Mode=Disable` is .NET-only; Rider ignores it):

```
jdbc:postgresql://172.27.104.244:5432/catalog?ssl=false&sslmode=disable
```

```bash
podman exec -it eshop_catalog_db psql -U catalog -d catalog
```

### Grafana LGTM (`lgtm`)

One container. Sign-in: **admin** / **admin**. Catalog still starts if LGTM is down.

| What | URL |
|------|-----|
| Grafana UI | http://172.27.104.244:3000 |
| OTLP HTTP | http://172.27.104.244:4318 |
| OTLP gRPC | http://172.27.104.244:4317 |

In-stack (Catalog compose): `http://lgtm:4318`. Host Catalog `appsettings.json` still uses `http://localhost:4318`.

Loki (`3100`), Tempo (`3200`), Prometheus (`9090`) stay inside the container. Query logs in Grafana: **Explore → Loki**.

```logql
{service_name=~".*eShop.Catalog.Api.*"}
| line_format "{{__line__}}\ntrace_id='{{.trace_id}}'\nspan_id='{{.span_id}}'"
```

`trace_id` / `span_id` are fields on **request** rows, not in the default message text. Startup lines have none. Catalog exports **logs**, not full traces, so Tempo can be empty.

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

New production C# should follow an existing spec (or a new one) and TDD.
