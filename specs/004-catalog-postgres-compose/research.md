# Research: Host Local Catalog Store and Catalog.Api

## 1. One compose pair vs separate stacks

**Decision**: One Tedu overlay pair: `deploy/compose.yaml` lists `catalogdb`, `catalogapi`, and `lgtm` (images, network `eshop`, volume names); `deploy/compose.override.yaml` configures those services (env, ports, healthcheck, `depends_on`, local build). Operators start only `catalogdb` when they need the store without Catalog or Grafana (`up -d --wait catalogdb`). Documented Catalog run is `up -d --wait catalogapi` (starts `catalogdb` as a dependency, not `lgtm`).

**Rationale**: Clarifications require Catalog.Api in the same compose pair (FR-015) and store-only start (FR-010, FR-016). Named-service `up` keeps Grafana optional without Compose profiles.

**Alternatives considered**:
- Sibling `deploy/catalog/` and `deploy/observability/` stacks — rejected; extra files and start commands.
- Compose profiles — extra concept; named-service `up catalogdb` / `up catalogapi` is enough.
- Install Postgres on the host — rejected by US1.
- Catalog only on the host (`dotnet run`) — rejected by Session 2026-10-29 (compose is the documented run path).

## 2. Postgres image and engine

**Decision**: Pin the official `postgres:16` image (tree currently has `postgres:16.15`; keep it pinned, same rule as `grafana/otel-lgtm:0.38.0`). One store service, published host port `5432`.

**Rationale**: Catalog already uses Marten 9, which speaks PostgreSQL. Pinning avoids a surprise major bump.

**Alternatives considered**:
- `postgres:17` — newer; no Catalog requirement for it.
- `pgvector` / Timescale images — unused features.
- Testcontainers in Catalog tests — rejected by FR-012 and feature 003.

## 3. Two connection views (in-stack vs published host)

**Decision**:

| View | Used by | Host | Port | Database / user / password |
|------|---------|------|------|----------------------------|
| In-stack | `catalogapi` compose service | `catalogdb` | `5432` | `catalog` / `catalog` / `catalog` |
| Published host | Developer on the workstation | `localhost` | `5432` | same |

- Catalog setting key remains `ConnectionStrings:CatalogDb` (do not invent a new key).
- Inject the in-stack value on `catalogapi` via `ConnectionStrings__CatalogDb=Host=catalogdb;Port=5432;Database=catalog;Username=catalog;Password=catalog`.
- Do not use host `appsettings.Development.json` / `appsettings.Development.local.json` as the documented success path (FR-006).
- Compose comments document both views (local-development defaults only; FR-013).

**Rationale**: Catalog inside the compose network cannot use `localhost` to reach `catalogdb`. A developer on the host cannot use the hostname `catalogdb` unless they add extra DNS. Same database identity in both views (clarification Option A).

**Landmine**: A gitignored `appsettings.Development.local.json` with empty `CatalogDb` only affects host Catalog, which is not this feature’s acceptance path. The container must not copy `appsettings.*.local.json` (already in `.dockerignore` and `CopyToPublishDirectory=Never`).

**Alternatives considered**:
- Single `localhost` string for Catalog.Api — fails inside the network unless `network_mode: host` (poor on Windows/Podman).
- Single in-stack string for everyone — breaks US1 host `pg_isready` / direct connection.
- Fill Development.json for `dotnet run` as the proof — rejected by FR-015 / FR-006.

## 4. Persistence across restart

**Decision**: Named Docker/Podman volume `catalog-pgdata` mounted at `/var/lib/postgresql/data`. Ordinary `compose stop` / `down` without `-v` keeps data. Do not add a factory-reset command in v1.

**Rationale**: FR-009 / SC-004. Named volume matches “stop does not delete data.”

**Alternatives considered**:
- Bind mount `./data` — extra gitignore and host permissions, especially on Windows.
- No volume (ephemeral) — fails FR-009.
- `compose down -v` as the documented stop — would wipe data.

## 5. Ready state (store and Catalog.Api)

**Decision**:
- `catalogdb` healthcheck: `pg_isready -U catalog -d catalog`. Store-only start: `up -d --wait catalogdb`.
- `catalogapi` `depends_on` `catalogdb` with `condition: service_healthy` (does **not** depend on `lgtm`).
- `catalogapi` healthcheck: TCP/listen on container port `8080` (no new Catalog HTTP health route). Documented Catalog start: `up -d --wait catalogapi`.
- Catalog is not considered ready until that `--wait` returns (store healthy, then Catalog listening).
- If the store is stopped after Catalog is up, `POST /products` must not return 201 (connection/store error). Compose `depends_on` does not stop Catalog when the store stops.

**Rationale**: FR-003, FR-016, US3. `--wait` is already used for `lgtm`. A new `/health` endpoint would be a new public surface not required by the spec.

**Alternatives considered**:
- No healthcheck, sleep N seconds — flaky on first pull/build.
- Catalog `/health` that pings Postgres — new route; extra test-seam risk for isolated tests.
- `depends_on: lgtm` — violates FR-010 / FR-018.

## 6. Empty database schema (Marten) vs isolated tests

**Decision**: In Catalog `AddMarten`, set `options.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate`. Do **not** call `ApplyAllDatabaseChangesOnStartup()`. Isolated tests keep the dummy `CatalogDb` + replaced `IDocumentSession` from feature 003.

**Rationale**: An empty `catalog` database has no Marten document tables. Auto-create on first session lets create-product succeed against a fresh store (US2) with one StoreOptions line. `ApplyAllDatabaseChangesOnStartup()` would open a real connection during host start and break `CatalogApiFactory` (dummy host `127.0.0.1`, user `x`). FR-012 forbids requiring this compose for existing checks.

**US3.3 mapping**: Store ready/not-ready is compose health / `pg_isready`. When the store is down, `POST /products` against still-running `catalogapi` must fail with a connection/store error (not 201). Catalog process start with a non-empty `CatalogDb` may still succeed because startup currently only validates that the setting is non-empty (feature 003). Changing that to connect-on-start is rejected.

**Alternatives considered**:
- `ApplyAllDatabaseChangesOnStartup()` + strip Marten hosted services in the test factory — extra test plumbing.
- Hand-written SQL init scripts duplicating Marten tables — drift.
- Leave AutoCreate at Marten default `None` — first create-product fails on a missing table.

## 7. Catalog.Api image and HTTP URL

**Decision**:
- Add `src/Services/eShop.Catalog.Api/Dockerfile`: multi-stage `mcr.microsoft.com/dotnet/sdk:10.0` build → `mcr.microsoft.com/dotnet/aspnet:10.0` runtime. Listen on container port `8080` (ASP.NET default).
- Build context: repository root (`..` from `deploy/`) so BuildingBlocks restore. `dockerfile`: `src/Services/eShop.Catalog.Api/Dockerfile`.
- `compose.yaml` image name: `eshop-catalog-api:local`. Local `build` lives in `compose.override.yaml` (Tedu overlay: images in the base file, local build/config in the override).
- Publish `5000:8080` so the documented Catalog local URL stays `http://localhost:5000` (FR-017). HTTPS is not published.
- Keep `UseHttpsRedirection()`; with HTTP-only listen it does not redirect. Do not add an HTTPS endpoint for this feature.
- `ASPNETCORE_ENVIRONMENT=Development` on the compose service is acceptable (Swagger may appear); CatalogDb comes from compose env, not from a host settings file.

**Rationale**: Clarification Option A for the client URL. Existing `.http` / create-product clients already use port 5000. Official ASP.NET container images listen on 8080.

**Alternatives considered**:
- Host `dotnet run` as the documented run — rejected by FR-015.
- Publish HTTPS `5050` — rejected; HTTPS is not required.
- Random host port — rejected; create-product proof would not be stable.
- Listen on 5000 inside the container — extra `ASPNETCORE_URLS` vs the image default; mapping `5000:8080` is enough.

## 8. In-stack diagnostics (OTLP) without requiring Grafana

**Decision**: Do not change `BuildBlock.Logging` or diagnostic record shape. On `catalogapi`, override the existing Serilog OpenTelemetry sink endpoint with compose env `Serilog__WriteTo__1__Args__endpoint=http://lgtm:4318` (WriteTo[0] is Console, WriteTo[1] is OpenTelemetry in `appsettings.json`). Do not `depends_on` `lgtm`. When `lgtm` is stopped, Catalog still starts and accepts create-product; sink failures must not block startup (same rule as feature 001). Leave `appsettings.json` default `http://localhost:4318` for non-compose runs.

**Rationale**: FR-018 / SC-007. Catalog inside compose cannot reach `localhost:4318` on the workstation. Spec 001 already allows override and requires start when the collector is down.

**Alternatives considered**:
- `depends_on: lgtm` — violates optional Grafana.
- `host.docker.internal:4318` — not the in-stack address; weaker on Linux/Podman.
- Change Catalog `appsettings.json` default to `http://lgtm:4318` — breaks host-process export to published OTLP.
- New logging configuration key — rejected; reuse the existing sink endpoint.

## 9. Testing and TDD

**Decision**: No new .NET test project and no Testcontainers. Validation is the quickstart against [contracts/catalog-postgres.md](./contracts/catalog-postgres.md):

1. `up -d --wait catalogdb` returns only when `pg_isready` succeeds; `lgtm` is not required.
2. Host connection `localhost:5432` with user/password/db `catalog` works.
3. `up -d --wait catalogapi` starts `catalogdb` if needed, does not start `lgtm`, and returns only when Catalog is listening.
4. `catalogapi` has in-stack `CatalogDb` (`Host=catalogdb`); `POST http://localhost:5000/products` returns 201 and the product id exists in the Marten document table.
5. After `compose restart catalogdb` (no `-v`), that id is still present.
6. With `catalogdb` stopped, `POST /products` does not return 201.
7. With `lgtm` running, create-product diagnostics appear in Grafana Loki; with `lgtm` stopped, Catalog still starts.
8. `dotnet test` on existing Catalog and BuildingBlock suites still passes with this compose stopped.

TDD for this feature: run those checks first (they fail because `catalogapi` / Dockerfile / healthcheck / in-stack env do not exist). Then add Dockerfile + compose service + env. Change Catalog `StoreOptions` only after the create-product-against-empty-store check fails for a missing schema. Do not edit isolated test projects unless a production line proves they break (then fix the factory, do not point tests at this Postgres).

**Rationale**: Workspace TDD applies to C# production logic. This feature is mostly deploy config, like 002. FR-012 is a completion gate.

**Alternatives considered**:
- xUnit + Testcontainers — rejected by FR-012 and 003.
- Keep `dotnet run` as a contract check — rejected by FR-015.

## 10. Observability, APIs, and leftovers

**Decision**: Keep `lgtm` in the same compose pair. Do not change create-product request path or public product APIs. Do not add read/update/delete. Local Grafana sign-in stays `admin` / `admin`. Catalog.Api stop can use ordinary `compose stop catalogapi` (not a separate spec FR).

**Rationale**: FR-011, spec out of scope, Ponytail.
