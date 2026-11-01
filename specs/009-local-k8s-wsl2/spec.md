# Feature Specification: Host the Local Product Stack on Kubernetes (WSL2)

**Feature Branch**: `009-local-k8s-wsl2`

**Created**: 2026-11-01

**Status**: Draft

**Input**: User description: "help me to host to k8s in local. I have the wsl2"

## Clarifications

### Session 2026-11-01

- Q: Which local Kubernetes should this project treat as the official WSL2 host path? → A: k3s installed in Ubuntu on WSL2 (containerd). One local cluster. Official documented path. Compose files are not deleted. k3d, kind, minikube, Docker Desktop Kubernetes, Docker, and Podman are not required.
- Q: Should the cluster Catalog store use the same disk as compose Catalog, or its own store? → A: Separate cluster store. Compose data stays on compose. k3s data stays on the cluster.
- Q: How should a client on the developer machine reach Catalog when it runs on k3s? → A: Same localhost HTTP URL that compose used. k3s is the only required runnable path that binds that URL.
- Q: When removing compose containers, is that only this eShop compose stack or every container on the machine? → A: Superseded. Running compose is not required. If leftover compose processes ever hold the Catalog URL, stop only this project’s compose stack.
- Q: Which compose services belong on k3s? → A: **All of them.** The official cluster start hosts the same identities as `deploy/compose.yaml`: catalog store, Catalog.Api, search-sync (kafka, connect, connect-register, elasticsearch, kibana, kafka-ui), viewer (lgtm), log-collector, and exporters (postgres-exporter, elasticsearch-exporter, kafka-exporter, cadvisor). Catalog Ready still waits only on the store.
- Q: Should each service run in its own cluster so they talk “like real life”? → A: **One k3s cluster, three namespaces** (`catalog`, `search`, `viewer`). Each compose service is its own Deployment + Service. Same-namespace DNS stays short (`catalogdb:5432`). Cross-namespace DNS uses `name.namespace.svc` (for example `kafka.search.svc:9092`, `elasticsearch.search.svc:9200`, `lgtm.viewer.svc:4318`). This laptop is 16 GB RAM — extra k3s clusters will not fit. No NetworkPolicy unless added later.
- Q: Update the official WSL2 host path from minikube to k3d? → A: Superseded. Official path is k3s in Ubuntu, not k3d.
- Q: With Docker gone, which remaining option should a WSL2 developer use so they can still start a local cluster? → A: Drop k3d. Install k3s directly in Ubuntu (containerd only). Docker and Podman are not required.
- Q: With Docker and Podman gone, what should this project do with the existing compose local path? → A: Keep compose files. k3s is the only required runnable local path. Running compose is not required.
- Q: On k3s, after Grafana and the metric exporters are ready, must the existing Ops/Infra dashboard show live metrics the way compose does? → A: Same as compose: Ops/Infra must show live metrics (healthy/not plus detail). Empty panels are a defect. Catalog still works if that metrics path is down.
- Q: Which product-stack containers must appear as healthy or not on that k3s Ops/Infra overview? → A: Same compose set: store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, and metric exporters. Collector and Grafana stay off the board.
- Q: If Kafka or search is still starting, does Ops/Infra still pass as long as every listed container appears? → A: Appear is enough: Ready = healthy; not ready/stopped = not healthy; empty panels fail. Do not wait for the whole search set before accepting the board.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run Catalog and the catalog store on a local cluster from WSL2 (Priority: P1)

A developer who already works in WSL2 starts a **k3s cluster path** documented in this project. They do not install a production cluster, a cloud account, Docker, or Podman. After the documented start finishes, Catalog is reachable on a documented local HTTP URL from their machine. They create a product and get it by id against the cluster catalog store. Compose files stay in the repo as reference; this feature does not require running compose.

**Why this priority**: “Host to k8s in local” is not delivered until Catalog and its store run on a local cluster and accept the same product writes the developer already uses.

**Independent Test**: From WSL2, follow the documented local-cluster start. Confirm Catalog becomes ready only after the store is ready (even if search or the viewer are still starting). Complete one valid create-product and one get-by-id on the documented local HTTP URL.

**Acceptance Scenarios**:

1. **Given** WSL2 and k3s in Ubuntu are available, **When** the developer runs the documented local-cluster start, **Then** the catalog store and Catalog become ready without a cloud cluster, Docker, or Podman. Catalog MUST NOT wait on the viewer or search to become Ready.
2. **Given** Catalog on the local cluster is ready, **When** a client on the developer machine submits a valid create-product request to the documented local HTTP URL, **Then** Catalog returns a new product identity and that product is stored in the cluster catalog store.
3. **Given** that product exists, **When** the client gets the product by id on the same documented URL, **Then** they receive the stored product.
4. **Given** the catalog store is not ready, **When** the developer inspects Catalog, **Then** Catalog MUST NOT appear available.

---

### User Story 2 - Host search-sync on the same local cluster (Priority: P2)

The documented k3s start hosts the same search-sync pieces as compose on the **same** local cluster (message broker, connector, search index, and the operator UIs they already use locally). After those parts are ready and at least one product has been synced, `GET /products` returns items. If search is down, create and get-by-id still work; browse returns the same “search unavailable” behavior as today’s local stack.

**Why this priority**: The cluster path must host the full product, not Catalog alone. Catalog writes still come first so Ready rules stay fail-open.

**Independent Test**: After the documented full-stack start, wait until search-sync is ready and a product has been synced, then browse returns items. Stop search and confirm create/get-by-id still succeed.

**Acceptance Scenarios**:

1. **Given** the documented local-cluster start has been run, **When** the developer waits for the search-sync set, **Then** those parts become ready on the same cluster without a second cluster or a second project.
2. **Given** search is ready and at least one product has been synced, **When** the developer calls browse on the documented Catalog URL, **Then** they receive items (empty items is valid if nothing has synced yet).
3. **Given** search is stopped, **When** the developer creates a product and gets it by id, **Then** those calls still succeed; browse fails in the same user-visible way as the current local stack.

---

### User Story 3 - Host the local viewer and collectors; product work stays fail-open (Priority: P2)

The documented k3s start hosts the existing local metrics/log viewer, log collector, and exporters on the same cluster. After the viewer and exporters are Ready for about one minute, the existing **Ops/Infra** view MUST show live metrics the same way compose does. The overview MUST list the same product-stack set: store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, and metric exporters. Each MUST appear. A listed container that is Ready MUST show healthy with samples. A listed container that is not Ready or is stopped MUST show not healthy. Collector and Grafana stay off this board. Empty metric panels on that view are a defect. Accepting Ops/Infra MUST NOT wait for the whole search set to become Ready. Catalog and Kafka-class product work MUST still become Ready if those pods are down. Isolated automated checks MUST pass without starting the local cluster.

**Why this priority**: The cluster path hosts the same observability identities as compose. It must not make Catalog wait on them.

**Independent Test**: After the documented full-stack start, wait about one minute after the viewer and exporters are Ready (search may still be starting), then open Ops/Infra and confirm every listed container appears: Ready ones healthy with samples, not-ready or stopped ones not healthy. Click a Ready container for detail metrics. Empty panels fail. Collector and Grafana need not appear. Catalog create and get-by-id still succeed if the viewer or collectors are not Ready. Run the existing isolated test suites without starting the cluster; they pass.

**Acceptance Scenarios**:

1. **Given** the viewer or collectors are not Ready, **When** Catalog and the store are Ready on the local cluster, **Then** Catalog accepts create and get-by-id.
2. **Given** the local viewer and metric exporters have been Ready for about one minute (search MAY still be starting), **When** the developer opens the existing Ops/Infra view, **Then** store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, and metric exporters all appear: Ready ones healthy with samples, not-ready or stopped ones not healthy. A click on a Ready container shows detail metrics. No new data source or second Grafana. Collector and Grafana are not required on the board. Empty metric panels fail this scenario.
3. **Given** no local cluster is running, **When** a developer runs the existing isolated Catalog and building-block test suites, **Then** those suites still pass.

---

### User Story 4 - Documented start and stop; compose files stay (Priority: P3)

A developer follows run notes to start, wait until ready, and stop the local k3s path from WSL2. Stopping the cluster path does not delete the compose files. Destroying cluster data is a documented, explicit action — not the daily stop. Running compose is not required to finish this feature.

**Why this priority**: Hosting is only usable if start/stop is written down. Compose files stay so the stack identities remain visible; they are not a second required run path.

**Independent Test**: Follow the run notes from a clean WSL2 session: start the documented full stack, confirm Catalog ready, stop without the destructive flag, confirm Catalog is no longer reachable, and confirm the compose files are still in the repo.

**Acceptance Scenarios**:

1. **Given** the run notes, **When** a developer on WSL2 follows start then wait-until-ready, **Then** they do not need undocumented flags, a second repository, Docker, or Podman.
2. **Given** the cluster path is running, **When** they use the documented ordinary stop, **Then** Catalog is no longer reachable and cluster data used for daily work is not wiped unless they chose the documented destructive stop.
3. **Given** the cluster path is stopped, **When** they inspect the repo, **Then** the existing compose files are still present and unchanged. Starting compose is out of scope for this feature.

---

### Edge Cases

- First start on a machine with no prior cluster data: empty catalog store is valid; Catalog must still become ready after the store is ready. Products that exist only in compose do not appear on k3s.
- WSL2 is up but the local cluster cannot start (k3s missing or not running): the developer sees a clear documented failure, not a silent hang.
- Catalog is started without the store: Catalog MUST NOT appear available.
- Search index missing (nothing synced yet): browse is empty success, not “search down,” matching current local behavior.
- Search process down: browse is unavailable; create and get-by-id still work.
- Local viewer down: product writes still work.
- Viewer and metric exporters Ready for about one minute but Ops/Infra panels are empty or “No data”: that is a defect, not a pass. A stopped or not-Ready product-stack container (store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, metric exporters) MUST show as not healthy (that is data, not an empty panel). Ops/Infra MAY pass while search is still starting. Collector and Grafana may be absent from the board. An empty catalog is not an outage.
- Ordinary stop vs destructive stop: daily stop does not wipe catalog data; destructive stop is explicit and documented.
- k3s is the only required runnable local path. This feature MUST NOT require starting compose, Docker, or Podman. Compose files stay in the repo.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The project MUST provide a documented **local Kubernetes host path** that a developer can start from WSL2 without a cloud account or a production cluster. The official path MUST be **k3s installed in Ubuntu on WSL2** (k3s uses containerd). k3d, kind, minikube, Docker Desktop Kubernetes, Docker, and Podman MUST NOT be required.
- **FR-002**: That path MUST host the catalog store and Catalog.Api. Catalog MUST become ready only after the store is ready.
- **FR-003**: Clients on the developer machine MUST reach Catalog on the **same documented local HTTP URL that compose used**. HTTPS and production certificates are out of scope. k3s is the only required path that binds that URL.
- **FR-004**: Create-product and get-by-id on that URL MUST persist in and read from the **cluster catalog store** (same product behavior as the current local Catalog). That store MUST be **separate** from the compose catalog store. Products created on compose MUST NOT appear on k3s unless the developer created them again on the cluster path. A one-time copy from compose is out of scope.
- **FR-005**: The existing compose files MUST remain in the repo. This feature MUST NOT delete them. Running compose MUST NOT be required. Docker and Podman MUST NOT be required.
- **FR-006**: Isolated and service-level automated checks MUST complete without starting the local cluster or the local viewer.
- **FR-007**: Catalog process start on the cluster path MUST NOT require the local metrics/log viewer or collectors.
- **FR-008**: The documented k3s start MUST host the existing search-sync product pieces (broker, connector, connector register, search index, operator UIs) on the **same** cluster, not a second cluster and not a later optional add-on.
- **FR-009**: When search is down, create and get-by-id MUST keep working. Browse MUST fail in the same user-visible way as the current local stack.
- **FR-010**: Run notes MUST tell a WSL2 developer how to start the **full compose-equivalent stack** on k3s, wait until Catalog is ready, stop, and (separately) destroy data. Those notes MUST NOT require Docker, Podman, or starting compose. Catalog Ready MUST NOT require the viewer or collectors.
- **FR-014**: The documented k3s start MUST host the compose observability identities on the same cluster: `lgtm`, `log-collector`, `postgres-exporter`, `elasticsearch-exporter`, `kafka-exporter`, and `cadvisor`. No second Grafana product. Catalog Ready MUST NOT wait on any of them.
- **FR-016**: After the local viewer and metric exporters have been Ready for about one minute, the existing **Ops/Infra** view MUST show live metrics the same way compose does. The overview MUST include store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, and metric exporters. Each listed container MUST appear. A listed container that is Ready MUST show healthy with samples. A listed container that is not Ready or is stopped MUST show not healthy. A click on a Ready container MUST open that container’s detail metrics. Accepting this view MUST NOT wait for the whole search set to become Ready. Collector and Grafana MUST NOT be required on this board. The developer MUST NOT add a data source or a second Grafana by hand. Empty metric panels on that view are a defect. Catalog create and get-by-id MUST still succeed if the metrics path is down.
- **FR-011**: Local-dev access MUST keep the existing no-auth local defaults. Production auth, TLS, and multi-tenant cluster security are out of scope.
- **FR-012**: This feature MUST NOT add a new public Catalog HTTP route and MUST NOT change create/get/browse contracts.
- **FR-013**: A later application service MUST be hostable on the same local cluster path without inventing a second local cluster product.
- **FR-015**: The one k3s cluster MUST use three namespaces: `catalog` (store + Catalog.Api), `search` (broker, connector, register, search index, operator UIs), and `viewer` (LGTM, log collector, exporters, cadvisor). Same-namespace calls MAY use the short Service name. Cross-namespace calls MUST use `name.namespace.svc`. Catalog Ready MUST NOT depend on the other namespaces being Ready.

### Key Entities

- **Local cluster path**: The documented k3s cluster installed and started in Ubuntu on WSL2. Distinct from the existing compose files. Not Docker Desktop Kubernetes, kind, minikube, or k3d. One cluster, three namespaces (`catalog`, `search`, `viewer`).
- **Catalog store (cluster)**: The catalog database instance in namespace `catalog`. Empty on first start is valid. **Not** the compose catalog disk. Compose and cluster stores MUST NOT share files.
- **Catalog.Api (cluster)**: The same Catalog application in namespace `catalog`, ready only after its store is ready, published on the **same local HTTP URL as compose**.
- **Search-sync set**: Broker, connector, connector register, search index, and related operator UIs in namespace `search`.
- **Local viewer and collectors**: The existing metrics/log UI (`lgtm`), log collector, and exporters in namespace `viewer`. Not a second product. Not a Catalog Ready gate. The existing **Ops/Infra** view is the operator metrics board; hosting the viewer without live metrics does not satisfy this feature.
- **Ops/Infra (k3s)**: The same Grafana metrics view used on compose. Overview set is store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, and metric exporters. Collector and Grafana are not on this board. After about one minute of Ready viewer and exporters, every listed container MUST appear. Ready → healthy with samples. Not Ready or stopped → not healthy. Empty / “No data” on required metric panels is a defect. Accepting the board MUST NOT wait for the whole search set.
- **Ordinary stop / destructive stop**: Ordinary stop ends processes and keeps daily catalog data. Destructive stop wipes cluster catalog data and is never the default daily command.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer on WSL2 who follows the run notes can have Catalog ready on the local cluster (store ready first) and complete one create-product plus one get-by-id in under 15 minutes on a machine that already has k3s in Ubuntu.
- **SC-002**: After Catalog is ready on the cluster path, 100% of valid create-product then get-by-id attempts against the documented local URL succeed while the store is up.
- **SC-003**: Existing isolated Catalog and building-block test suites still pass with the local cluster stopped (same suites as today).
- **SC-004**: A developer can complete create and get-by-id on the first attempt after Catalog and the store are Ready, even if the viewer or collectors are not Ready.
- **SC-005**: After the documented ordinary stop, the documented Catalog URL does not accept new product creates, and the compose files are still present and unchanged.
- **SC-006**: When the search-sync set on the cluster has been healthy for one minute and at least one product has been synced, a developer can browse products on the same Catalog URL in under 2 minutes.
- **SC-007**: When the local viewer and metric exporters on k3s have been Ready for one minute, a developer opening Ops/Infra sees every listed container (store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, metric exporters) within 2 minutes without adding a data source: Ready ones healthy with samples, not-ready or stopped ones not healthy, plus at least one Ready detail view with samples. This MUST succeed even if search is still starting. Empty required panels fail this criterion. Collector and Grafana need not appear. Create and get-by-id still succeed if that metrics path is stopped.

## Assumptions

- The developer works in **WSL2** on Windows. The official cluster is **k3s installed in Ubuntu** (containerd). Docker and Podman are not required for the cluster path.
- This feature is **local development only**. Cloud Kubernetes, production hardening, and ingress certificates are out of scope.
- **Compose files stay** as reference. k3s is the only required runnable local path. Running compose is not required and is out of scope for this feature.
- Compose catalog data (`catalog-pgdata`) and k3s catalog data are **separate**. No shared disk and no required copy step.
- Catalog public HTTP behavior, local-dev credentials, and fail-open rules from earlier features stay the same.
- Isolated `dotnet test` remains the default quality gate and does not start a cluster.
- The official k3s start hosts **every** compose service identity. Search-sync, viewer, collector, and exporters are part of that start, not a later add-on. Catalog Ready still waits only on the store. After the viewer and exporters are Ready, the existing Ops/Infra view MUST list the compose product-stack set with live healthy/not; empty panels are a defect. The board MAY pass while search is still starting if not-ready listed containers show not healthy.
- Local defaults stay no-auth HTTP, same as compose.
- A single local cluster is enough; a second local Kubernetes product is out of scope. Isolation is three namespaces, not extra k3s clusters.
