# Quickstart: Host Local Catalog Store and Catalog.Api

Validation guide for `004-catalog-postgres-compose`. Implementation steps belong in `tasks.md` (not created by `/speckit-plan`).

## Prerequisites

- Podman or Docker with Compose (same runtime as the log viewer service)
- .NET 10 SDK (for isolated `dotnet test` only; documented Catalog run is compose)
- Repo files (after implement):
  - [deploy/compose.yaml](../../deploy/compose.yaml) (`version: '3.9'`, `catalogdb` + `catalogapi` + `lgtm`, default network `eshop`, volume names)
  - [deploy/compose.override.yaml](../../deploy/compose.override.yaml) (`container_name`, credentials, ports, volume, healthchecks, `catalogapi` build and in-stack env, `depends_on`)
  - [src/Services/eShop.Catalog.Api/Dockerfile](../../src/Services/eShop.Catalog.Api/Dockerfile)
- Spec and contracts:
  - [spec.md](./spec.md)
  - [contracts/catalog-postgres.md](./contracts/catalog-postgres.md)
  - [data-model.md](./data-model.md)
- Grafana/Loki is optional. Store-only: `catalogdb`. Documented Catalog run: `catalogapi` (starts the store, not Grafana).

## Start the catalog store (must wait until Ready)

From the repo root:

```bash
podman compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait catalogdb
# or: docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait catalogdb
```

Expected: the command returns only after `pg_isready -U catalog -d catalog` succeeds. `catalogapi` and `lgtm` are not required.

Published local store address (local defaults only):

```text
Host=localhost;Port=5432;Database=catalog;Username=catalog;Password=catalog
```

In-stack store address (used by `catalogapi`, not by a developer on the host):

```text
Host=catalogdb;Port=5432;Database=catalog;Username=catalog;Password=catalog
```

## Prove Catalog.Api from compose

```bash
podman compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait catalogapi
# or: docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait catalogapi
```

Expected:

- Command returns only after the store is healthy and Catalog is listening
- Store starts if it was not already running
- `lgtm` is not required
- Catalog local URL: `http://localhost:5000`
- Do not run `dotnet run` for this check

Then `POST http://localhost:5000/products` with a valid create-product body ([contracts/catalog-postgres.md](./contracts/catalog-postgres.md) §4).

Expected:

- HTTP 201 and a non-empty `id`
- That `id` exists in the local store (Marten product document)

## Prove restart keeps data

Restart only `catalogdb` (do not pass `-v`). Wait until Ready. Look up the same `id`.

Expected: the product is still present.

## Prove not-ready is obvious

```bash
podman compose -f deploy/compose.yaml -f deploy/compose.override.yaml stop catalogdb
# or: docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml stop catalogdb
```

Expected: `pg_isready` fails; `POST http://localhost:5000/products` does not return 201 (connection/store error). Ordinary stop does not wipe the volume.

Restart with `up -d --wait catalogapi` (or `up -d --wait catalogdb` then wait for Catalog). The same in-stack connection string still applies inside `catalogapi`.

## Optional: diagnostics when the log viewer is up

```bash
podman compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait lgtm
```

Grafana UI: `http://localhost:3000` (admin / admin). Repeat create-product. Expected: Loki shows Catalog records (`{service_name=~".*eShop.Catalog.Api.*"}`). Catalog compose env uses `http://lgtm:4318`, not `http://localhost:4318`.

With `lgtm` stopped, `up -d --wait catalogapi` and create-product still succeed.

## Isolated checks still pass without this store

With catalog compose stopped:

```bash
dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj
dotnet test tests/BuildingBlocks/BuildingBlock.Tests/BuildingBlock.Tests.csproj
```

Expected: previously passing tests still pass. No Grafana. No this Postgres. No `catalogapi`.

## Out of scope for this validation

- Host `dotnet run` of Catalog as the documented run
- Changing `lgtm` Loki activation behavior
- New Catalog read/update/delete APIs
- Testcontainers or pointing the xUnit suite at this database
- Production or shared cloud Postgres
- HTTPS on Catalog.Api
- `compose down -v` as the daily stop (that deletes local data)
