# Tasks: Host the Local Product Stack on Kubernetes (WSL2)

**Input**: Design documents from `/specs/009-local-k8s-wsl2/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included. Workspace TDD and plan require failing isolated file-parse checks **before** changing `deploy/k8s/**` or README run notes. Isolated tests MUST NOT start k3s, MUST NOT `kubectl apply`, MUST NOT run `docker compose`, MUST NOT call `k3s ctr`. Operator apply/forward is [quickstart.md](./quickstart.md) only — do not run it unless the user asks. Do not add a new `.csproj`. Do not add Helm. Do not add a new Catalog HTTP route. Do not edit `deploy/compose.yaml` or `deploy/compose.override.yaml`. Do not mount compose volume `catalog-pgdata`. Do not make Catalog Ready wait on lgtm / kafka / elasticsearch. Do not require Docker, Podman, minikube, or k3d. Do not commit unless the user asks.

**Organization**: Tasks are grouped by user story so each story can be implemented and tested independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- Include exact file paths in descriptions

## Path Conventions

- Cluster: `deploy/k8s/` (official apply: `kubectl apply -k deploy/k8s`)
- Catalog overlay: `deploy/k8s/catalog/`
- Search overlay: `deploy/k8s/search/`
- Viewer overlay: `deploy/k8s/viewer/`
- Host URL script: `deploy/k8s/port-forward-localhost.sh`
- Compose (do not change): `deploy/compose.yaml`, `deploy/compose.override.yaml`
- Isolated tests: `tests/Services/eShop.Catalog.Api.Tests/LocalK8sRulesTests.cs`
- Run notes: `README.md`
- Classroom notes: `specs/009-local-k8s-wsl2/notes-how-it-works.md`
- Contracts: `specs/009-local-k8s-wsl2/contracts/` (Ops/Infra: `contracts/k3s-ops-infra.md`)
- Validation: `specs/009-local-k8s-wsl2/quickstart.md`
- Existing Ops/Infra dashboard (do not fork): `deploy/grafana/provisioning/dashboards/ops-infra.json`
- k3s Prometheus scrape (new): `deploy/k8s/viewer/prometheus.yaml`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Keep the kustomize tree next to compose. No new project. No compose edits.

- [X] T001 Confirm directories `deploy/k8s/`, `deploy/k8s/catalog/`, `deploy/k8s/search/`, and `deploy/k8s/viewer/` exist per `specs/009-local-k8s-wsl2/plan.md`; do **not** add a new `.csproj`; do **not** edit `deploy/compose.yaml` or `deploy/compose.override.yaml`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Namespaces `catalog`, `search`, `viewer` and isolated-test rules that forbid k3s/Docker. Blocks every story apply.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T002 Isolated tests in `tests/Services/eShop.Catalog.Api.Tests/LocalK8sRulesTests.cs`: `deploy/k8s/namespace.yaml` declares Kind `Namespace` for `catalog`, `search`, and `viewer` (data-model.md Namespaces; FR-015); this test class MUST NOT start k3s, MUST NOT call `kubectl apply`, MUST NOT run `docker compose`, MUST NOT call `k3s ctr` (FR-006). Isolated tests MUST NOT start Docker or Podman. Rewrite any remaining minikube / `docker compose down` assertions in this class as they are touched. Test MUST fail until T003 exists if `namespace.yaml` is missing

- [X] T003 Write or confirm `deploy/k8s/namespace.yaml`: three Kind `Namespace` objects named `catalog`, `search`, and `viewer` (contracts/k3s-catalog.md Apply; data-model.md Namespaces; FR-015)

**Checkpoint**: Isolated T002 is green. Do not start k3s.

---

## Phase 3: User Story 1 - Run Catalog and the catalog store on a local cluster from WSL2 (Priority: P1) 🎯 MVP

**Goal**: P1 overlay hosts `catalogdb` + `catalogapi` in namespace `catalog`. Catalog Ready only after store Ready. Host URL is `deploy/k8s/port-forward-localhost.sh` → `http://localhost:5000`. Cluster store is PVC `catalogdb-data` on k3s `local-path`, not compose `catalog-pgdata`. Catalog image is local-only (`imagePullPolicy: Never`).

**Independent Test**: Isolated `LocalK8sRulesTests` pass without a cluster. Operator (later, full stack): `dotnet publish` + `k3s ctr images import`, `kubectl apply -f deploy/k8s/namespace.yaml && kubectl apply -k deploy/k8s`, port-forward script, create + get-by-id on `http://localhost:5000`. Catalog Ready must not wait on viewer or search. No Docker.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T004 [US1] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/LocalK8sRulesTests.cs` that `deploy/k8s/catalog/catalogdb-pvc.yaml` is Kind `PersistentVolumeClaim`, name `catalogdb-data`, namespace `catalog`, access `ReadWriteOnce`, size at least `1Gi`, StorageClass `local-path` (or cluster default `local-path`), and the catalog overlay MUST NOT reference Docker volume `catalog-pgdata` or a host bind of that volume (data-model.md PersistentVolumeClaim `catalogdb-data`: Access `ReadWriteOnce`; Size at least `1Gi`; StorageClass k3s `local-path`; Mount catalogdb container path `/var/lib/postgresql/data`). MUST fail until T008 matches

- [X] T005 [US1] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/LocalK8sRulesTests.cs` that `deploy/k8s/catalog/catalogdb.yaml` declares Deployment + Service `catalogdb`: image `postgres:16.15`, replicas `1`, env `POSTGRES_USER=catalog`, `POSTGRES_PASSWORD=catalog`, `POSTGRES_DB=catalog`, command extra `postgres -c wal_level=logical`, Service ClusterIP port `5432`, readiness `pg_isready -U catalog -d catalog`, volume PVC `catalogdb-data` mounted at `/var/lib/postgresql/data` (data-model.md Deployment + Service `catalogdb`). MUST fail until T009 matches

- [X] T006 [US1] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/LocalK8sRulesTests.cs` that `deploy/k8s/catalog/catalogapi.yaml` declares Deployment + Service `catalogapi`: image `eshop-catalog-api:local`, `imagePullPolicy` `Never`, replicas `1`, container port `8080`, Service ClusterIP `catalogapi:8080` (MUST NOT be NodePort 5000), env `ConnectionStrings__CatalogDb=Host=catalogdb;Port=5432;Database=catalog;Username=catalog;Password=catalog`, initContainer waits until store accepts connections, readiness HTTP or TCP on `8080`, start gate store only — no wait on lgtm / kafka / elasticsearch (data-model.md Deployment + Service `catalogapi`; contracts/k3s-catalog.md Ready / Host URL / Image). Replace the old `IfNotPresent` assert. MUST fail until T010 matches

- [X] T007 [US1] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/LocalK8sRulesTests.cs` that `deploy/k8s/catalog/kustomization.yaml` lists `catalogdb-pvc.yaml`, `catalogdb.yaml`, and `catalogapi.yaml` in namespace `catalog` (plan.md `deploy/k8s/catalog/`; contracts/k3s-catalog.md Apply). MUST fail until T011 matches

### Implementation for User Story 1

- [X] T008 [P] [US1] Write or update `deploy/k8s/catalog/catalogdb-pvc.yaml`: PVC `catalogdb-data` in namespace `catalog`, `ReadWriteOnce`, at least `1Gi`, StorageClass `local-path`; MUST NOT name or bind `catalog-pgdata` (data-model.md PersistentVolumeClaim `catalogdb-data`)

- [X] T009 [P] [US1] Write or update `deploy/k8s/catalog/catalogdb.yaml`: Deployment + Service `catalogdb`, image `postgres:16.15`, replicas `1`, `POSTGRES_USER=catalog` / `POSTGRES_PASSWORD=catalog` / `POSTGRES_DB=catalog`, `postgres -c wal_level=logical`, ClusterIP `5432`, readiness `pg_isready -U catalog -d catalog`, mount PVC `catalogdb-data` at `/var/lib/postgresql/data` (data-model.md Deployment + Service `catalogdb`)

- [X] T010 [P] [US1] Write or update `deploy/k8s/catalog/catalogapi.yaml`: Deployment + Service `catalogapi` in namespace `catalog`, image `eshop-catalog-api:local`, `imagePullPolicy: Never`, replicas `1`, port `8080`, ClusterIP (not NodePort 5000), `ConnectionStrings__CatalogDb=Host=catalogdb;Port=5432;Database=catalog;Username=catalog;Password=catalog`, optional fail-open env `Elasticsearch__Uri=http://elasticsearch.search.svc:9200` and `Kafka__BootstrapServers=kafka.search.svc:9092` and `OTEL_EXPORTER_OTLP_ENDPOINT=http://lgtm.viewer.svc:4318`, initContainer waits for `catalogdb:5432` (`pg_isready -h catalogdb -U catalog -d catalog`), readiness on `8080`, no init/start-gate on lgtm / kafka / elasticsearch (contracts/k3s-catalog.md; FR-002, FR-007, FR-012, FR-015)

- [X] T011 [US1] Write or update `deploy/k8s/catalog/kustomization.yaml` listing `catalogdb-pvc.yaml`, `catalogdb.yaml`, `catalogapi.yaml`, namespace `catalog` (research.md §7)

**Checkpoint**: Isolated T004–T007 are green. Catalog overlay is apply-ready. Do not start k3s unless the user asks.

---

## Phase 4: User Story 2 - Host search-sync on the same local cluster (Priority: P2)

**Goal**: Overlay `deploy/k8s/search/` hosts the compose search-sync set (kafka, connect, connect-register, elasticsearch, kibana, kafka-ui) in namespace `search`. Required on the documented start. Catalog Ready if those pods are down.

**Independent Test**: Isolated tests pass with catalog + search files. Catalog start-gate still store-only. Operator later: official `kubectl apply -k deploy/k8s`; browse after sync; create/get-by-id still work if search pods are deleted.

### Tests for User Story 2 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T012 [US2] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/LocalK8sRulesTests.cs` that `deploy/k8s/search/kustomization.yaml` applies in namespace `search` and the overlay uses the same images as `deploy/compose.yaml`: `apache/kafka:3.8.1`, `quay.io/debezium/connect:2.7.3.Final`, `docker.elastic.co/elasticsearch/elasticsearch:8.15.3`, `docker.elastic.co/kibana/kibana:8.15.3`, `provectuslabs/kafka-ui:v0.7.2` (contracts/k3s-search.md Search overlay; FR-008). MUST fail until T014–T018 exist

- [X] T013 [US2] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/LocalK8sRulesTests.cs` that `deploy/k8s/catalog/catalogapi.yaml` start-gate / `initContainers` still do **not** require kafka, connect, or elasticsearch (contracts/k3s-search.md: Catalog Deployment MUST still become Ready if these pods are down; FR-009). MUST fail if T010 added a search wait

### Implementation for User Story 2

- [X] T014 [P] [US2] Write or update `deploy/k8s/search/kafka.yaml`: Deployment + Service `kafka`, image `apache/kafka:3.8.1`, in-cluster listener `kafka:9092`, namespace `search` (same role as compose `kafka` in `deploy/compose.override.yaml`)

- [X] T015 [P] [US2] Write or update `deploy/k8s/search/elasticsearch.yaml` and `deploy/k8s/search/kibana.yaml`: images `docker.elastic.co/elasticsearch/elasticsearch:8.15.3` and `docker.elastic.co/kibana/kibana:8.15.3`, Services in namespace `search`

- [X] T016 [P] [US2] Write or update `deploy/k8s/search/connect.yaml`: Deployment + Job/Pod for `connect` + `connect-register` using `quay.io/debezium/connect:2.7.3.Final`, `BOOTSTRAP_SERVERS` `kafka:9092`, store host `catalogdb.catalog.svc`, Catalog host `catalogapi.catalog.svc`, namespace `search` (same CDC register role as compose)

- [X] T017 [P] [US2] Write or update `deploy/k8s/search/kafka-ui.yaml`: image `provectuslabs/kafka-ui:v0.7.2`, namespace `search`

- [X] T018 [US2] Write or update `deploy/k8s/search/kustomization.yaml` listing kafka, connect, elasticsearch, kibana, kafka-ui resources, namespace `search` (plan.md P2 `deploy/k8s/search/`; FR-013 same cluster; FR-015)

**Checkpoint**: Isolated T012–T013 green. Catalog overlay still has no search wait. Do not start k3s unless asked.

---

## Phase 5: User Story 3 - Host viewer, collectors, and exporters; Catalog stays fail-open (Priority: P2)

**Goal**: Overlay `deploy/k8s/viewer/` hosts compose observability identities: `lgtm`, `log-collector`, `postgres-exporter`, `elasticsearch-exporter`, `kafka-exporter`, `cadvisor`. cadvisor uses the **k3s containerd** socket. After viewer + exporters Ready, existing **Ops/Infra** MUST show live healthy/not for `catalogdb`, `catalogapi`, `kafka`, `connect`, `elasticsearch`, `kibana`, `kafka-ui`, `elasticsearch-exporter`, `kafka-exporter`, `postgres-exporter` (FR-016). Empty panels are a defect. Collector and Grafana stay off the board. Catalog Ready if those pods are down. Isolated Catalog / building-block suites still pass with the cluster stopped.

**Independent Test**: Isolated `LocalK8sRulesTests` plus existing Catalog and BuildingBlock suites pass without k3s. Catalog manifests do not wait on lgtm. Root `deploy/k8s/kustomization.yaml` includes catalog, search, and viewer. Alloy and cadvisor MUST NOT require `docker.sock`. Viewer overlay MUST mount a Prometheus scrape file (exporter + cadvisor targets) and Ops/Infra, and MUST relabel cadvisor onto `compose_service`.

### Tests for User Story 3 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T019 [US3] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/LocalK8sRulesTests.cs` that `deploy/k8s/viewer/` uses compose images `grafana/otel-lgtm:0.38.0`, `grafana/alloy:v1.21.3`, `quay.io/prometheuscommunity/postgres-exporter:v0.17.1`, `quay.io/prometheuscommunity/elasticsearch-exporter:v1.8.0`, `danielqsj/kafka-exporter:v1.11.0`, `ghcr.io/google/cadvisor:v0.60.6`; Alloy config is `deploy/k8s/viewer/config.alloy` (k8s discovery, not compose docker.sock); cadvisor mounts `/run/k3s/containerd/containerd.sock` and MUST NOT require `/var/run/docker.sock`; `catalogapi.yaml` MUST NOT init-wait lgtm / OTLP; `deploy/k8s/kustomization.yaml` includes `catalog/`, `search/`, and `viewer/` (contracts/k3s-search.md Viewer overlay; research.md §10; FR-007, FR-014). MUST fail until T021–T026 match

- [X] T020 [US3] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/LocalK8sRulesTests.cs` that the test source itself does not invoke `k3s` start, `kubectl apply`, `docker compose`, or `k3s ctr` (FR-006, SC-003). Existing isolated Catalog and BuildingBlock suites stay in their current projects — do **not** edit `tests/BuildingBlocks/BuildBlock.Logging.Tests`

### Implementation for User Story 3

- [X] T021 [P] [US3] Write or update `deploy/k8s/viewer/lgtm.yaml`: Deployment + Service using `grafana/otel-lgtm:0.38.0`, namespace `viewer`; no second Grafana product (FR-007, FR-014; contracts/k3s-search.md)

- [X] T022 [US3] Write or update `deploy/k8s/viewer/kustomization.yaml` listing lgtm, log-collector, exporters, and cadvisor, namespace `viewer`; do **not** add viewer resources into `deploy/k8s/catalog/kustomization.yaml`

- [X] T023 [US3] Confirm `deploy/k8s/catalog/catalogapi.yaml` has no initContainer / readiness gate on lgtm and does not require `OTEL_EXPORTER_OTLP_ENDPOINT` for Ready (data-model.md catalogapi: Start gate store only). Edit only if T010 accidentally gated on OTLP

- [X] T024 [P] [US3] Write or update `deploy/k8s/viewer/exporters.yaml`: Deployments + Services in namespace `viewer` for `postgres-exporter` (`quay.io/prometheuscommunity/postgres-exporter:v0.17.1`, `DATA_SOURCE_NAME` to `catalogdb.catalog.svc` / database `catalog`), `elasticsearch-exporter` (`elasticsearch.search.svc`), `kafka-exporter` (`kafka.search.svc`), and `cadvisor` (`ghcr.io/google/cadvisor:v0.60.6`) mounting `/run/k3s/containerd/containerd.sock` (MUST NOT mount `/var/run/docker.sock`); Catalog MUST NOT wait on them (FR-014, FR-015; research.md §10)

- [X] T025 [P] [US3] Write or update `deploy/k8s/viewer/log-collector.yaml` and `deploy/k8s/viewer/config.alloy`: image `grafana/alloy:v1.21.3`; Kubernetes pod discovery in namespaces `catalog`, `search`, and `viewer`; MUST NOT require compose `docker.sock` (research.md §10)

- [X] T026 [US3] Write or update `deploy/k8s/kustomization.yaml` including `catalog/`, `search/`, and `viewer/` so official apply is `kubectl apply -k deploy/k8s` (contracts/k3s-search.md Official apply; FR-008, FR-014)

**Checkpoint (hosting)**: Isolated T019–T020 green. Root kustomization lists the full stack. Existing Catalog / BuildingBlock isolated tests still pass with k3s stopped.

### Tests for User Story 3 — Ops/Infra live metrics (FR-016) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation.** Hosting LGTM (T021) is not enough.

- [X] T033 [US3] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/LocalK8sRulesTests.cs` that `deploy/k8s/viewer/prometheus.yaml` (or the scrape file mounted into `lgtm`) contains jobs targeting `postgres-exporter:9187`, `elasticsearch-exporter:9114`, `kafka-exporter:9308`, and `cadvisor:8080`, and that job `containers` relabels the Kubernetes container name onto `compose_service` for `catalogdb|catalogapi|kafka|connect|elasticsearch|kibana|kafka-ui|elasticsearch-exporter|kafka-exporter|postgres-exporter` (contracts/k3s-ops-infra.md §2; research.md §12; data-model.md Ops/Infra scrape jobs). MUST fail until T035 matches

- [X] T034 [US3] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/LocalK8sRulesTests.cs` that `deploy/k8s/viewer/lgtm.yaml` and/or `deploy/k8s/viewer/kustomization.yaml` mount a Prometheus scrape file at `/otel-lgtm/prometheus.yaml` and mount existing `deploy/grafana/provisioning/dashboards/ops-infra.json` (uid `eshop-ops-infra`) at the same Grafana provision path compose uses; `deploy/k8s/catalog/catalogapi.yaml` MUST still have no init/start-gate on lgtm (contracts/k3s-ops-infra.md §1 / §4; FR-007, FR-016). MUST NOT require a second Grafana. MUST fail until T036 matches

### Implementation for User Story 3 — Ops/Infra live metrics (FR-016)

- [X] T035 [P] [US3] Write `deploy/k8s/viewer/prometheus.yaml`: keep LGTM OTLP and self-scrape settings; jobs `catalogdb` → `postgres-exporter:9187`, `elasticsearch` → `elasticsearch-exporter:9114`, `kafka` → `kafka-exporter:9308`, `containers` → `cadvisor:8080`; `metric_relabel_configs` on `containers` copy Kubernetes container name onto `compose_service` for the overview set (research.md §12; data-model.md: Overview set is store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, exporters). MUST NOT edit `deploy/compose.yaml`, `deploy/compose.override.yaml`, or `deploy/prometheus/prometheus.yaml`. MUST NOT add Prometheus Operator

- [X] T036 [US3] Update `deploy/k8s/viewer/lgtm.yaml` and `deploy/k8s/viewer/kustomization.yaml` so `lgtm` mounts `deploy/k8s/viewer/prometheus.yaml` at `/otel-lgtm/prometheus.yaml` and mounts `deploy/grafana/provisioning/dashboards/ops-infra.json` at the same Grafana provision path compose uses; no second Grafana; do not fork `ops-infra.json` (contracts/k3s-ops-infra.md §1; FR-014, FR-016)

- [X] T037 [US3] Confirm container names in `deploy/k8s/catalog/catalogdb.yaml`, `deploy/k8s/catalog/catalogapi.yaml`, `deploy/k8s/search/*.yaml`, and `deploy/k8s/viewer/exporters.yaml` match compose identities `catalogdb`, `catalogapi`, `kafka`, `connect`, `elasticsearch`, `kibana`, `kafka-ui`, `elasticsearch-exporter`, `kafka-exporter`, `postgres-exporter` so the `compose_service` relabel can hit them (data-model.md Overview set). Edit a container name only if it does not match

**Checkpoint (FR-016)**: Isolated T033–T034 green. Catalog still has no viewer start-gate. Do not start k3s unless the user asks.

---

## Phase 6: User Story 4 - Documented start and stop; compose files stay (Priority: P3)

**Goal**: README tells a WSL2 developer how to use **k3s in Ubuntu**, import the Catalog image without Docker, apply the **full stack**, wait Catalog Ready, run `deploy/k8s/port-forward-localhost.sh`, check Ops/Infra live metrics, ordinary-stop vs destroy. Compose files stay. Running compose is not required.

**Independent Test**: Isolated tests assert the documented commands in `README.md`. Operator follows [quickstart.md](./quickstart.md) later. After ordinary stop, compose files are still in the repo.

### Tests for User Story 4 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T027 [US4] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/LocalK8sRulesTests.cs` that `README.md` documents: official cluster is k3s in Ubuntu (`INSTALL_K3S_EXEC="--disable traefik --disable servicelb"`); Docker, Podman, minikube, k3d, and Docker Desktop Kubernetes are not required; image path `dotnet publish` `/t:PublishContainer` + `k3s ctr images import` for `eshop-catalog-api:local`; `kubectl apply -f deploy/k8s/namespace.yaml` and official `kubectl apply -k deploy/k8s`; `deploy/k8s/port-forward-localhost.sh` and/or `kubectl -n catalog port-forward svc/catalogapi 5000:8080`; `http://localhost:5000`; ordinary stop is end the forward and/or `sudo systemctl stop k3s` and keeps PVC `catalogdb-data`; destructive stop is `kubectl delete pvc catalogdb-data -n catalog`; namespaces `catalog` / `search` / `viewer`; compose files stay and running compose is not required; Catalog Ready does not require viewer/search (FR-001, FR-005, FR-008, FR-010, FR-014; contracts/k3s-catalog.md Image; contracts/compose-files.md). MUST NOT require `docker compose` as a k3s start/stop step. MUST fail until T028 exists

### Implementation for User Story 4

- [X] T028 [US4] Replace the `README.md` “Run on minikube” section with a WSL2 k3s section covering T027’s commands; keep compose files documented as reference only; do not tell the operator to `docker compose up` / `down` / `build` for the k3s path; host URL is the port-forward script (research.md §1–§3, §6, §11; FR-003, FR-005, FR-010)

- [X] T029 [US4] Align `specs/009-local-k8s-wsl2/notes-how-it-works.md` with the implemented k3s commands (`dotnet publish` + `k3s ctr`, `systemctl stop k3s`, PVC `catalogdb-data`, no Docker) so the classroom notes match `README.md` and [quickstart.md](./quickstart.md)

- [X] T038 [US4] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/LocalK8sRulesTests.cs` that `README.md` documents Ops/Infra at `http://localhost:3000/d/eshop-ops-infra/ops-infra` after viewer and exporters have been Ready about one minute, empty panels are a defect, search MAY still be starting, and Catalog create/get-by-id still work if the metrics path is down (FR-016, SC-007; contracts/k3s-ops-infra.md). MUST fail until T039 matches

- [X] T039 [US4] Update `README.md` with the Ops/Infra live-metrics check from `specs/009-local-k8s-wsl2/quickstart.md` §5 and `specs/009-local-k8s-wsl2/contracts/k3s-ops-infra.md`; keep compose files as reference only; do not require `docker compose`; do not add a second Grafana

- [X] T041 [US4] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/LocalK8sRulesTests.cs` that `README.md` documents Catalog redeploy: `-t:PublishContainer`, `-p:ContainerRepository=eshop-catalog-api`, `k3s ctr images import`, `rollout restart deploy/catalogapi`, `imagePullPolicy: Never`, and `wsl.exe -d Ubuntu -u root` (FR-017). MUST fail until T042 matches

- [X] T042 [US4] Add a README Catalog redeploy script: publish tar with `-t:`/`-p:` (Git Bash), import `eshop-catalog-api:local`, then `rollout restart deploy/catalogapi` because the same tag with `Never` does not recreate the pod; Windows password skip is `wsl.exe -d Ubuntu -u root` (research.md §3; FR-017)

- [X] T043 [US4] Isolated test in `tests/Services/eShop.Catalog.Api.Tests/LocalK8sRulesTests.cs` that `README.md` documents deploy-all: `kubectl apply -k deploy/k8s` plus rollout/wait for `catalogdb`, `catalogapi`, `kafka`, `connect`, `connect-register`, `elasticsearch`, `kibana`, `kafka-ui`, `lgtm`, `log-collector`, `postgres-exporter`, `elasticsearch-exporter`, `kafka-exporter`, and `cadvisor` (FR-018). MUST fail until T044 matches

- [X] T044 [US4] Expand README k3s start into a deploy-all script covering every compose identity in namespaces `catalog`, `search`, and `viewer` (FR-008, FR-014, FR-018; contracts/k3s-search.md). Catalog Ready wait stays store-only; remaining rollouts come after.

**Checkpoint**: Isolated T027 and T038 green. Compose files still present and unchanged.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Prove isolated suites. Do not apply the cluster unless the user asks.

- [X] T030 Confirm `deploy/compose.yaml` and `deploy/compose.override.yaml` are unchanged (FR-005); confirm no Helm chart and no second cluster product under `deploy/`

- [X] T031 Run isolated `dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj --filter FullyQualifiedName~LocalK8s` and the existing Catalog isolated suites; k3s MUST stay stopped (SC-003, quickstart.md §1)

- [X] T032 Leave operator cluster steps in `specs/009-local-k8s-wsl2/quickstart.md` for a later session; do **not** run `k3s` install/start, `k3s ctr`, or `kubectl apply` unless the user asks

- [X] T040 Run isolated `dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj --filter FullyQualifiedName~LocalK8s` after T033–T039; k3s MUST stay stopped (SC-003, FR-006)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational — MVP
- **User Story 2 (Phase 4)**: Depends on US1 catalog manifests (fail-open assertions on `catalogapi.yaml`)
- **User Story 3 (Phase 5)**: Depends on US1 catalog overlay and US2 search overlay (root kustomization includes both). FR-016 tasks T033–T037 depend on T021/T024 (LGTM + exporters already hosted)
- **User Story 4 (Phase 6)**: Depends on official apply command (`-k deploy/k8s`); T038–T039 depend on T033–T036 so README describes a real scrape/dashboard mount
- **Polish (Phase 7)**: T040 depends on T033–T039

### User Story Dependencies

- **User Story 1 (P1)**: After Foundational. No other stories. Catalog+store increment.
- **User Story 2 (P2)**: After US1 `catalogapi.yaml` exists. Required for documented start.
- **User Story 3 (P2)**: After US1 (and T018 for root include). Required for documented start. Catalog fail-open. cadvisor on k3s containerd. FR-016: LGTM scrape + Ops/Infra mount + `compose_service` relabel.
- **User Story 4 (P3)**: After T026. Documents k3s start/stop and Ops/Infra live-metrics check. Compose files stay.

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- PVC / Deployments before kustomization
- Story complete before moving to the next priority (unless staffed in parallel after US1)

### Parallel Opportunities

- T008, T009, T010 after T004–T007 are written (different YAML files)
- T014, T015, T016, T017 after T012–T013 (different search YAML files)
- T021, T024, T025 after T019 (different viewer YAML files)
- T035 after T033 (prometheus.yaml is a new file)
- US2 YAML and US3 YAML can proceed in parallel after US1; T026 waits for both overlays
- Do **not** parallelize T002 / T004–T007 / T012–T013 / T019–T020 / T027 / T033–T034 / T038 — they share `LocalK8sRulesTests.cs`

---

## Parallel Example: User Story 1

```text
# After failing T004–T007 exist, implement YAML in parallel:
Task: "Write deploy/k8s/catalog/catalogdb-pvc.yaml"
Task: "Write deploy/k8s/catalog/catalogdb.yaml"
Task: "Write deploy/k8s/catalog/catalogapi.yaml"
# Then T011 kustomization.yaml
```

## Parallel Example: User Story 3 FR-016

```text
# After failing T033–T034 exist:
Task: "Write deploy/k8s/viewer/prometheus.yaml"
# Then T036 mounts (lgtm.yaml + kustomization.yaml)
# Then T037 container-name confirm
```

---

## Implementation Strategy

### MVP First (User Story 1 increment)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1 (Catalog + store YAML, `imagePullPolicy: Never`)
4. **VALIDATE**: Isolated Catalog `LocalK8s` tests green
5. Continue US2 + US3 — required for the documented start
6. US4 README documents k3s (no Docker)

### Incremental Delivery

1. Setup + Foundational → namespace + test class rules
2. US1 → Catalog + store overlay + Never pull policy
3. US2 → search overlay
4. US3 → viewer overlay + k3s containerd cadvisor + root kustomization + FR-016 scrape/dashboard mounts
5. US4 → README k3s start/stop + Ops/Infra check; compose files stay
6. Documented apply is always the root overlay; Catalog Ready never waits on US2/US3 pods

### Parallel Team Strategy

1. Shared: T001–T003
2. After T011: one person README (US4), one person search (US2), one person viewer (US3)

---

## Notes

- Isolated tests parse files only. They never start k3s.
- Host port 5000 is port-forward, not NodePort.
- Catalog image: `dotnet publish` + `k3s ctr images import`. No Docker.
- Ordinary stop is `systemctl stop k3s`. Do not uninstall k3s daily.
- Ops/Infra on k3s reuses `ops-infra.json`. Relabel cadvisor → `compose_service`. Do not fork the dashboard.
- Do not commit unless the user asks.
