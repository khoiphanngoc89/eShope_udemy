# Implementation Plan: Host Local Catalog Store and Catalog.Api

**Branch**: `004-catalog-postgres-compose` | **Date**: 2026-10-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-catalog-postgres-compose/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Give developers a one-command local catalog store and a Catalog.Api compose service so they can persist create-product without installing Postgres or running Catalog on the host. Grafana/Loki stay optional (`up catalogdb` only). The documented Catalog run is `up --wait catalogapi`, which starts the store if needed and becomes ready only after the store is ready.

Technical approach: one Tedu overlay pair — `deploy/compose.yaml` lists `catalogdb`, `catalogapi`, and `lgtm`; `deploy/compose.override.yaml` configures them. `catalogdb` is pinned `postgres:16.x`, named volume `catalog-pgdata`, local role/database `catalog`, host port `5432`. `catalogapi` builds from `src/Services/eShop.Catalog.Api/Dockerfile`, maps host `5000` to container `8080`, injects in-stack `ConnectionStrings__CatalogDb` (`Host=catalogdb`), and depends on healthy `catalogdb` only (not `lgtm`). In-stack OTLP is `http://lgtm:4318` via compose env. One Marten line: `AutoCreate.CreateOrUpdate`. Do not apply schema at process start. Do not change create-product path or diagnostic record shape. Host Catalog is not this feature’s acceptance path.

## Technical Context

**Language/Version**: Compose YAML 3.9 (Podman or Docker Compose). C# / .NET 10.0 for Catalog Dockerfile, `StoreOptions`, and compose-injected settings.

**Primary Dependencies**: Official `postgres:16` (pin published 16.x; current tree has `16.15`); `mcr.microsoft.com/dotnet/sdk:10.0` + `aspnet:10.0`; existing Marten 9.54.2; Catalog `ConnectionStrings:CatalogDb`; Serilog OpenTelemetry sink (existing)

**Storage**: Local PostgreSQL database `catalog`. In-stack host `catalogdb:5432`. Published host view `localhost:5432`. Named volume `catalog-pgdata`. Not the isolated in-suite store used by Catalog tests.

**Testing**: Compose/HTTP/`pg_isready` checks in [quickstart.md](./quickstart.md) against [contracts/catalog-postgres.md](./contracts/catalog-postgres.md). No new test project. Existing `eShop.Catalog.Api.Tests` and `BuildingBlock.Tests` must still pass with this compose stopped (FR-012).

**Target Platform**: Local developer workstation (Windows/Linux) running Podman or Docker. Documented Catalog runtime is the `catalogapi` compose service.

**Project Type**: Local compose stack (store + Catalog.Api + optional log viewer) plus one Marten AutoCreate line

**Performance Goals**: Store ready in under 2 minutes after first one-time setup wait (SC-001). Documented Catalog start ready only when the store is ready (SC-002). Create-product against `http://localhost:5000` persists (SC-003). Restart keeps 100% of stored products (SC-004).

**Constraints**: Do not require Grafana/Loki (FR-010). Do not change create-product path or diagnostic record shape (FR-011). Do not point existing automated checks at this Postgres (FR-012). Local credentials only (FR-013). No production/cloud database (FR-014). Catalog.Api is in the same compose pair; host Catalog is not acceptance (FR-015). Catalog ready only after store ready; store-only start remains valid (FR-016). Documented client URL is HTTP `http://localhost:5000` (FR-017). In-stack OTLP when `lgtm` is up; Catalog still starts if `lgtm` is down (FR-018). No new architectural layers. One compose.yaml + one compose.override.yaml only.

**Scale/Scope**: One compose pair (`catalogdb` + `catalogapi` + `lgtm`), one Catalog Dockerfile, two connection-string views, one Marten AutoCreate line, one host HTTP mapping `5000:8080`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` is still the unratified placeholder. Gates below use that template’s example principles plus the workspace SDD / TDD / simplicity rules.

| Gate | Result | Evidence |
|------|--------|----------|
| Library-first | N/A | No new library. Feature is local deploy config, one Catalog Dockerfile, compose-injected settings, and existing Catalog Marten options. |
| Test-first (non-negotiable) | PASS | Quickstart/contract checks are the Red phase. Catalog `StoreOptions` changes only after create-product against empty Postgres fails for missing schema. Isolated xUnit suites stay green without this store. |
| Integration / contract tests | PASS | Public contract is [contracts/catalog-postgres.md](./contracts/catalog-postgres.md): store `--wait`, two connection views, `up --wait catalogapi`, `POST /products` on `http://localhost:5000`, restart, stop, optional `lgtm`, isolated tests. |
| Observability | PASS | Diagnostic record shape unchanged (FR-011). Compose overrides OTLP to `http://lgtm:4318`. `catalogapi` does not depend on `lgtm` (FR-010, FR-018). |
| Simplicity / abstraction ban | PASS | One compose pair, one volume, one Dockerfile, reuse `CatalogDb`. No Testcontainers, no new health API, no schema SQL scripts, no new config key. |
| CLI interface | N/A | Constitution not ratified; operators use existing `podman compose` / `docker compose`. |
| Spec-first | PASS | Spec exists at `specs/004-catalog-postgres-compose/spec.md` with Session 2026-10-29 clarifications. |

**Post-design re-check**: PASS. Phase 1 keeps a compose contract (store + Catalog.Api), a store/setting/product/Catalog-service model, and a curl/`pg_isready`/`dotnet test` quickstart. That adds no code layer beyond one Dockerfile, compose service wiring, one Marten AutoCreate setting, and environment overrides. Isolated tests remain on the in-suite store. Host `dotnet run` is not a contract check.

## Project Structure

### Documentation (this feature)

```text
specs/004-catalog-postgres-compose/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── catalog-postgres.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
deploy/
├── compose.yaml                    # version 3.9; catalogdb + catalogapi + lgtm; network eshop; volume names
└── compose.override.yaml           # container_name, env, ports, volume, healthcheck, depends_on, build, restart

src/Services/eShop.Catalog.Api/
├── Dockerfile                      # multi-stage sdk:10.0 → aspnet:10.0; listen 8080
└── DependencyInjection.cs          # Marten AutoCreate.CreateOrUpdate only (no ApplyAllDatabaseChangesOnStartup)

.dockerignore                       # keep excluding bin/obj/local settings; do not exclude src/

tests/Services/eShop.Catalog.Api.Tests/   # unchanged unless AutoCreate proves a break; then factory only, still no live Postgres
```

**Structure Decision**: One Tedu overlay pair under `deploy/`. Catalog.Api is a compose service built from a Dockerfile next to the Catalog project. Do not add a new test project. Do not keep a separate `deploy/observability/` or `deploy/catalog/` compose stack. Do not treat host `dotnet run` as the documented Catalog run.

## Complexity Tracking

> No constitution violations to justify.
