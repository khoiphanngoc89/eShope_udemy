# Implementation Plan: Host the Local Product Stack on Kubernetes (WSL2)

**Branch**: `009-local-k8s-wsl2` | **Date**: 2026-11-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/009-local-k8s-wsl2/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Give a WSL2 developer a **k3s** path that hosts the **full compose-equivalent stack** so `http://localhost:5000` create/get-by-id works without a cloud cluster, Docker, or Podman. Official apply is `kubectl apply -k deploy/k8s` (catalog + search + viewer/collectors). Compose **files stay**; running compose is out of scope. Cluster Postgres is a **new PVC**, not `catalog-pgdata`. Isolated `dotnet test` does not start k3s. After LGTM and the metric exporters are Ready, the existing **Ops/Infra** view MUST show live healthy/not for the compose product-stack set (FR-016 / SC-007). Empty panels are a defect. Catalog Ready still does not wait on that path.

Technical approach: k3s in Ubuntu (`--disable traefik --disable servicelb`); plain YAML under `deploy/k8s/` (kustomize, **no Helm**); three namespaces `catalog` / `search` / `viewer`; Catalog image `eshop-catalog-api:local` via `dotnet publish /t:PublishContainer` + `k3s ctr images import` (no Docker); ClusterIP Services; **`deploy/k8s/port-forward-localhost.sh`** for the compose host URL; Catalog Ready only after store Ready (initContainer + probes). Cross-namespace DNS is `name.namespace.svc`. Search-sync, LGTM, Alloy, and exporters use the same images as `deploy/compose.yaml`. LGTM mounts a k3s Prometheus scrape file plus the existing Ops/Infra dashboard; cadvisor series are relabeled onto `compose_service` so the compose dashboard keeps working. No new Catalog HTTP route. No new production C#. No NetworkPolicy. No second Grafana.

Classroom sketch: [notes-how-it-works.md](./notes-how-it-works.md).

## Technical Context

**Language/Version**: Kubernetes YAML (kustomize). C# / .NET 10.0 Catalog image unchanged (listen 8080). k3s + kubectl from Ubuntu WSL2.

**Primary Dependencies**: k3s (containerd); .NET SDK for the local Catalog image tar; existing compose **images** (`postgres:16.15`, `eshop-catalog-api:local`, kafka, connect, elasticsearch, kibana, kafka-ui, lgtm, alloy, exporters, cadvisor). Compose files are reference only.

**Storage**: Cluster PVC `catalogdb-data` in namespace `catalog` (k3s `local-path`). **Not** compose volume `catalog-pgdata`. Local role/database `catalog` / `catalog` / `catalog`.

**Testing**: Isolated file-parse tests in `tests/Services/eShop.Catalog.Api.Tests` (manifests + README; **no k3s in CI**). Existing Catalog / BuildingBlock / logging suites stay green with cluster stopped. Operator validation in [quickstart.md](./quickstart.md).

**Target Platform**: Windows + Ubuntu WSL2 + k3s. Official cluster is k3s. Docker, Podman, k3d, kind, minikube, Docker Desktop Kubernetes not required.

**Project Type**: Local cluster deploy config + README. No new microservice.

**Performance Goals**: Catalog ready (store first) + one create + get-by-id in under 15 minutes on a machine that already has k3s in Ubuntu (SC-001). Isolated tests pass without the cluster (SC-003). After viewer + exporters Ready for one minute, Ops/Infra shows the listed containers (Ready = healthy with samples; not-ready = not healthy) within 2 minutes (SC-007).

**Constraints**: Same Catalog URL compose used (`http://localhost:5000`). Compose files stay; do not run compose. Fail-open: Catalog does not wait on viewer/search. No new public Catalog route. No production TLS. No Helm. No second Grafana. No second dashboard fork. No Prometheus Operator. No second cluster product. No Docker/Podman required. Empty Ops/Infra panels after viewer + exporters Ready are a defect.

**Scale/Scope**: Official start = every compose service identity on one k3s cluster in three namespaces. Incremental build: US1 `catalog` ns, US2 `search` ns, US3 `viewer` ns (now includes live Ops/Infra scrape + dashboard mounts), then root `deploy/k8s/kustomization.yaml`. P3 = README start/stop/destroy (k3s, not compose).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` is still the unratified placeholder. Gates use that template’s example principles plus workspace SDD / TDD / simplicity rules.

| Gate | Result | Evidence |
|------|--------|----------|
| Library-first | N/A | No new library. Deploy YAML + README + isolated file-parse tests. |
| Test-first (non-negotiable) | PASS | Isolated tests assert manifests (images, probes, port 5000, no Grafana gate, no compose volume mount) **before** apply. Isolated suites do not start k3s (FR-006). Existing minikube/compose string asserts must be rewritten in implement **before** README/manifest drift. |
| Integration / contract tests | PASS | Operator contract is [contracts/k3s-catalog.md](./contracts/k3s-catalog.md). Compose files contract is [contracts/compose-files.md](./contracts/compose-files.md). Full-stack contract is [contracts/k3s-search.md](./contracts/k3s-search.md). Ops/Infra live metrics contract is [contracts/k3s-ops-infra.md](./contracts/k3s-ops-infra.md). |
| Observability | PASS | Official apply includes LGTM, Alloy, and exporters (FR-014). LGTM MUST scrape those exporters and show the existing Ops/Infra board (FR-016). Catalog env must not require OTLP to become Ready (FR-007). |
| Simplicity / abstraction ban | PASS | Flat kustomize, no Helm, no operator, no new HTTP route, no new C# layer. Host URL reuses the existing port-forward script. Image import uses tools already required (dotnet + k3s). |
| CLI interface | N/A | Operators use k3s kubectl + dotnet publish. No Docker CLI. |
| Spec-first | PASS | Spec + Session 2026-11-01 clarifications (k3s, no Docker/Podman, compose files only, Ops/Infra live metrics on k3s). |

**Post-design re-check**: PASS. Official apply hosts the full compose identity set on one k3s cluster in namespaces `catalog`, `search`, and `viewer`. Catalog start-gate remains store-only. LGTM mounts scrape + Ops/Infra; cadvisor is relabeled to `compose_service`. Cross-namespace DNS only. Isolated tests remain file-parse + existing suites. No Docker/Podman/k3d path. No second Grafana.

## Project Structure

### Documentation (this feature)

```text
specs/009-local-k8s-wsl2/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── notes-how-it-works.md
├── contracts/
│   ├── k3s-catalog.md
│   ├── k3s-search.md
│   ├── k3s-ops-infra.md
│   └── compose-files.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
deploy/k8s/
├── kustomization.yaml             # official apply: all overlays
├── namespace.yaml                 # Namespaces catalog, search, viewer
├── port-forward-localhost.sh      # host URL http://localhost:5000
├── catalog/                       # ns catalog: store + Catalog
│   ├── kustomization.yaml
│   ├── catalogdb-pvc.yaml
│   ├── catalogdb.yaml
│   └── catalogapi.yaml
├── search/                        # kafka, connect, register, es, kibana, kafka-ui
│   └── …
└── viewer/                        # lgtm, log-collector, exporters, cadvisor
    ├── lgtm.yaml                  # mounts prometheus + Ops/Infra
    ├── prometheus.yaml            # scrape exporters/cadvisor; relabel compose_service
    └── …

deploy/compose.yaml                # unchanged (reference only)
deploy/compose.override.yaml       # unchanged

README.md                          # WSL2 k3s run notes (P3)

tests/Services/eShop.Catalog.Api.Tests/
└── LocalK8sRulesTests.cs          # file-parse manifests + README; no k3s
```

**Structure Decision**: Keep `deploy/k8s/` next to existing `deploy/compose.yaml`. Do not add a new .csproj. Do not put manifests under `src/`. Do not edit compose files.

## Complexity Tracking

> No constitution violations. Port-forward is the smallest way to keep host port 5000 without Docker or k3s ServiceLB.
