# Contract: Local Catalog compose (store + Catalog.Api)

Developers start and stop the catalog store and Catalog.Api with Compose. These checks are the machine-checkable contract. The documented Catalog run is the `catalogapi` compose service, not a host process. Do not require the log viewer service except for §8.

Compose files follow the Tedu overlay pattern (`version: '3.9'`). `deploy/compose.yaml` lists `catalogdb`, `catalogapi`, and `lgtm` (images), default network `eshop`, and volume names. `deploy/compose.override.yaml` configures those services (`catalogdb` `container_name` `eshop_catalog_db`, credentials, host port `5432`, volume mount, `pg_isready` healthcheck, `restart`; `catalogapi` local build, in-stack `CatalogDb`, host port `5000`, `depends_on` healthy `catalogdb`, listen healthcheck; `lgtm` Grafana ports and healthcheck).

Auth / connection (local defaults only, FR-013):

| Item | Value |
|------|--------|
| Database | `catalog` |
| Username | `catalog` |
| Password | `catalog` |
| In-stack store address | `Host=catalogdb;Port=5432;Database=catalog;Username=catalog;Password=catalog` |
| Published local store address | `Host=localhost;Port=5432;Database=catalog;Username=catalog;Password=catalog` |
| Catalog setting key | `ConnectionStrings:CatalogDb` |
| Catalog.Api injection | compose env `ConnectionStrings__CatalogDb` = in-stack store address |
| Catalog local URL | `http://localhost:5000` (HTTP only) |
| In-stack log-viewer address | `http://lgtm:4318` |

## 1. Store becomes Ready (FR-001, FR-003, FR-010)

From the repo root:

```bash
podman compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait catalogdb
# or: docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait catalogdb
```

**Success**

- Command returns 0 only after healthcheck `pg_isready -U catalog -d catalog` succeeds
- Log viewer service (`lgtm`) is not required (this command starts `catalogdb` only)
- `catalogapi` is not required (store-only start remains valid)
- Database `catalog` exists (empty of products on first start)

**Failure**

- Command returns before Postgres accepts connections
- Operator had to install Postgres on the host
- Command also started `lgtm` or `catalogapi` as a hard requirement

## 2. Store accepts the published host connection (FR-006)

```bash
pg_isready -h localhost -p 5432 -U catalog -d catalog
```

Or the same check via `compose exec` on `catalogdb` if `pg_isready` is not on the host.

**Success**

- Ready
- Connection with the documented username, password, and database name works from the host

**Failure**

- Connection refused, auth failure, or unknown database

## 3. Catalog.Api compose service starts with the store (FR-007, FR-015, FR-016, FR-017)

```bash
podman compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait catalogapi
# or: docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait catalogapi
```

**Success**

- Command returns 0 only after `catalogdb` is healthy and `catalogapi` is listening
- `catalogdb` was started if it was not already running
- `lgtm` is not required
- Catalog is reachable at `http://localhost:5000`
- `catalogapi` environment has in-stack `ConnectionStrings__CatalogDb` (`Host=catalogdb;...`), not `Host=localhost`
- Host `dotnet run` was not used

**Failure**

- Command returns while the store is still starting
- Catalog appears available before the store is ready
- Operator had to fill a host Catalog settings file
- Command required `lgtm`

## 4. Create-product persists in this store (FR-008, FR-011)

Reuse the public create path from `003-catalog-buildingblocks-tests` (live store, not the in-suite substitute):

```http
POST /products
Content-Type: application/json
```

Base URL: `http://localhost:5000`

**Body**: `name`, `price`, `description`, `imageFile`, `categories` (all required). See `specs/003-catalog-buildingblocks-tests/contracts/create-product-api.md`.

**Success**

- Status: `201 Created`
- Body includes non-empty `id`
- The local catalog store contains a Marten product document for that `id` (`mt_doc_product` unless implement-time naming differs)

**Failure of the feature**

- Non-201
- 201 but no row/document for that `id` in the local store
- Create path changed (not `POST /products`)

## 5. Restart keeps products (FR-009)

1. Complete §4 and record `id`.
2. Restart only the catalog store compose service (`catalogdb`, no `-v`).
3. Wait until the store is Ready again (`up -d --wait catalogdb` or healthcheck).
4. Look up that `id` in the store.
5. If `catalogapi` is still running, `POST /products` may be used again after the store is Ready; the recorded `id` must still exist in the store either way.

**Success**

- Same product document is still present

**Failure**

- Empty database or missing `id` after restart

## 6. Stopped store is obviously not ready (FR-002, US3)

```bash
podman compose -f deploy/compose.yaml -f deploy/compose.override.yaml stop catalogdb
# or: docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml stop catalogdb
```

**Success**

- Documented status / `pg_isready` shows not ready
- `POST /products` to a still-running `catalogapi` (or a newly started `catalogapi` that cannot reach a ready store) does **not** return 201; the failure is a connection/store error
- Ordinary stop does not require `down -v`

## 7. Isolated checks do not need this store (FR-012)

With this compose **stopped**:

```bash
dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj
dotnet test tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj
```

**Success**

- Previously passing tests still pass
- Suites do not start `deploy/compose.yaml` or `catalogapi`

## 8. Log viewer is optional; in-stack diagnostics when it is up (FR-010, FR-018)

**Without `lgtm`**

- §3 and §4 still succeed

**With `lgtm` running** (`up -d --wait lgtm` in addition, same compose pair)

- `catalogapi` is given in-stack OTLP `http://lgtm:4318`
- After §4, Grafana Loki can show Catalog diagnostic records for that request (`{service_name=~".*eShop.Catalog.Api.*"}`)
- Create-product path is still `POST /products`

**Failure of the feature**

- Catalog refuses to start because `lgtm` is stopped
- `catalogapi` still sends to `http://localhost:4318` (host view) as its compose setting

## Out of scope for this contract

- Host `dotnet run` of Catalog as an acceptance check
- Publishing Postgres as a production/cloud database
- `GET` / `PUT` / `DELETE` `/products`
- Changing `lgtm` image, Grafana ports, or Loki activation behavior (owned by `002-activate-loki-grafana`)
- Pointing Catalog automated tests at this Postgres
- HTTPS on Catalog.Api
