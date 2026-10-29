# Data Model: Host Local Catalog Store and Catalog.Api

This feature does not add Catalog product fields. Product remains the entity from Catalog and from `003-catalog-buildingblocks-tests`. The model here is the local store, the Catalog compose service that uses it, and the two connection views.

## Local Catalog Store

The dedicated local PostgreSQL database started by the compose stack (FR-001, FR-004, FR-005).

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| Engine | string | yes | PostgreSQL 16 (pinned image) |
| Database name | string | yes | `catalog` |
| Role | string | yes | `catalog` |
| Password | string | yes | Local default `catalog`; labeled local-development only (FR-013) |
| In-stack host | string | yes | Compose service name `catalogdb`, port `5432` |
| Published host | string | yes | `localhost`, port `5432` |
| Data volume | named volume | yes | `catalog-pgdata`; survives ordinary stop/restart (FR-009) |

### States

```text
Stopped → Starting → Ready
```

| State | Meaning | Developer sees |
|-------|---------|----------------|
| Stopped | Compose service not running | `pg_isready` / published `5432` fail; documented status is not ready |
| Starting | Container up; Postgres not accepting connections | `compose up --wait catalogdb` still blocking; healthcheck failing |
| Ready | `pg_isready -U catalog -d catalog` succeeds; `--wait` has returned | Catalog.Api may become ready; host can use the published local store address |

### Validation

- First successful `compose up --wait catalogdb`: state is **Ready**. Database `catalog` exists; no hand-created SQL (US1).
- `compose stop` or `down` without `-v`: state **Stopped**; volume still holds products (FR-009).
- `down -v` is not the ordinary stop; it deletes the volume (out of v1 reset scope).

## Catalog Store Setting

The existing Catalog configuration value that must be non-empty for startup (already enforced) and that points Catalog at this store (FR-006, FR-007).

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| Key | string | yes | `ConnectionStrings:CatalogDb` (do not invent a new key) |
| In-stack value | Npgsql connection string | yes (compose env) | `Host=catalogdb;Port=5432;Database=catalog;Username=catalog;Password=catalog` |
| Published host value | Npgsql connection string | yes (docs only) | `Host=localhost;Port=5432;Database=catalog;Username=catalog;Password=catalog` |
| Injection | compose environment | yes | `ConnectionStrings__CatalogDb` on `catalogapi`. Host settings files are not the documented success path. |

### Relationships

- One **Local Catalog Store** per developer workstation for this stack.
- One **Catalog Store Setting** on the Catalog compose service points at that store via the in-stack view.
- Catalog **Product** documents are stored in that database by Marten (table `mt_doc_product` unless Marten naming differs at implement time).

## Catalog Compose Service

Catalog.Api running in the same compose pair (FR-015, FR-016, FR-017, FR-018).

| Field | Type | Required | Rules |
|-------|------|----------|--------|
| Compose service | string | yes | `catalogapi` |
| Image | string | yes | `eshop-catalog-api:local`, built from `src/Services/eShop.Catalog.Api/Dockerfile` |
| Catalog local URL | string | yes | `http://localhost:5000` (host `5000` → container `8080`). HTTPS not published. |
| Store dependency | service | yes | `depends_on` healthy `catalogdb`. Must not depend on `lgtm`. |
| In-stack log-viewer address | string | yes | `http://lgtm:4318` when `lgtm` is running; must not block start when `lgtm` is stopped |

### States

```text
Stopped → Starting (wait for store Ready) → Ready
```

| State | Meaning | Developer sees |
|-------|---------|----------------|
| Stopped | `catalogapi` not running | `http://localhost:5000/products` fails to connect |
| Starting | Store not ready or Catalog not yet listening | `up --wait catalogapi` still blocking; Catalog must not appear available |
| Ready | Store is Ready and Catalog is listening on 8080; `--wait` has returned | Create-product on `http://localhost:5000` is allowed |

### Validation

- `up -d --wait catalogapi` starts `catalogdb` if needed and returns only when Catalog is Ready (FR-016).
- `up -d --wait catalogdb` does not start `catalogapi` (store-only remains valid).
- `up -d --wait catalogapi` does not require `lgtm`.
- Host Catalog process is not a state of this entity and is not required for acceptance.

## Product (existing, not changed)

Proof entity for US2 / FR-008. Fields unchanged: identity, name, price, description, image file, categories.

### Rules

- A valid create-product action against the Catalog compose service using this store MUST persist a Product whose id is the returned identity (FR-008).
- After store restart, that Product MUST still be loadable from the store (FR-009, SC-004).
- Isolated automated checks keep using an in-suite store, not this database (FR-012).
