# Research: Host the Local Product Stack on Kubernetes (WSL2)

## 0. One cluster, not one cluster per service

**Decision**: **One** k3s cluster in Ubuntu on WSL2. Three namespaces: `catalog`, `search`, `viewer`. Each compose identity is a Deployment + Service. Same-namespace DNS is the short Service name. Cross-namespace DNS is `name.namespace.svc`. Do **not** start extra k3s clusters.

**Rationale**: Clarify 2026-11-01. This machine is about 16 GB RAM. Namespaces add isolation with no extra control-plane RAM. Extra clusters will not fit next to Kafka + Elasticsearch + LGTM.

**Alternatives considered**:
- One namespace `eshop` — simpler DNS; user chose three namespaces.
- Extra k3s clusters — too heavy on 16 GB.
- One cluster per compose service — will not run.

## 1. Official cluster: k3s in Ubuntu, no Docker

**Decision**: Documented cluster is **k3s installed in Ubuntu on WSL2**. k3s uses **containerd**. kubectl is `k3s kubectl` (or a kubeconfig pointing at `/etc/rancher/k3s/k3s.yaml`). Docker, Podman, k3d, kind, minikube, and Docker Desktop Kubernetes are not required and not documented as the official path.

Install (systemd must be enabled in WSL2):

```bash
curl -sfL https://get.k3s.io | INSTALL_K3S_EXEC="--disable traefik --disable servicelb" sh -
```

Disable Traefik and ServiceLB: this path uses ClusterIP + `deploy/k8s/port-forward-localhost.sh` for host ports. Keep the built-in **local-path** provisioner (PVC).

If `systemctl` is missing, set Ubuntu `wsl.conf`:

```ini
[boot]
systemd=true
```

then restart the distro and retry install.

**Rationale**: User cannot run Docker on this laptop. k3d and Podman still need a container engine. k3s is Kubernetes + containerd in the existing Ubuntu distro. No extra WSL VM.

**Alternatives considered**:
- minikube + Docker driver — rejected (Docker gone).
- k3d — rejected (needs Docker or Podman).
- Podman in Ubuntu — extra engine; user declined.
- Docker Engine in Ubuntu — rejected (laptop cannot handle Docker).
- Docker Desktop Kubernetes — uninstalled.

## 2. Publish Catalog at http://localhost:5000

**Decision**: `catalogapi` Service stays **ClusterIP** port 8080. Host URL is produced by the existing script:

```bash
# from WSL2; keep it running
./deploy/k8s/port-forward-localhost.sh
```

That script already forwards Catalog `5000:8080` (and the operator UIs). If Windows `localhost:5000` fails, the script uses `--address 0.0.0.0` or curl from WSL2.

**Rationale**: Kubernetes NodePort cannot be 5000. Spec requires the same URL compose used. Port-forward needs no Docker and no k3s ServiceLB. Isolated tests already expect this script.

**Alternatives considered**:
- NodePort 5000 — invalid range.
- k3s ServiceLB LoadBalancer on 5000 — works, but extra k3s component and YAML churn; port-forward already exists.
- Ingress + Traefik on :80 — not port 5000; extra RAM.

## 3. Catalog image without Docker

**Decision**: Public images (`postgres:16.15`, kafka, elasticsearch, …) are **pulled by k3s/containerd**. The local Catalog image is built with the **.NET SDK only**, then imported into k3s:

```bash
dotnet publish src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj \
  -c Release \
  /t:PublishContainer \
  /p:ContainerRepository=eshop-catalog-api \
  /p:ContainerImageTag=local \
  /p:ContainerArchiveOutputPath=./eshop-catalog-api-local.tar
sudo k3s ctr images import ./eshop-catalog-api-local.tar
```

Catalog Deployment: `image: eshop-catalog-api:local`, `imagePullPolicy: Never` (do not hit Docker Hub for a local tag).

This is the same published app as `src/Services/eShop.Catalog.Api/Dockerfile` (aspnet 10.0, listen 8080). Isolated tests MUST NOT run publish, `k3s ctr`, or `kubectl apply`.

**Rationale**: Spec forbids requiring Docker or Podman. `ContainerArchiveOutputPath` writes an OCI tar without a daemon. `k3s ctr` is already on the machine after k3s install.

**Alternatives considered**:
- `docker compose build` + `minikube image load` — needs Docker; rejected.
- nerdctl + buildkit against the k3s socket — extra binaries.
- buildah — extra engine.
- HostPath of `dotnet publish` output — not a container image; more YAML special cases.

## 4. Separate cluster store (PVC)

**Decision**: PVC `catalogdb-data` in namespace `catalog`, StorageClass `local-path` (k3s default), mount `/var/lib/postgresql/data`. Do **not** mount compose `catalog-pgdata`. Postgres image/command/env match compose (`postgres:16.15`, `wal_level=logical`, user/db `catalog`). Ordinary stop keeps the PVC. Destructive stop deletes the PVC (or wipes k3s storage).

**Rationale**: Clarify: separate store. k3s local-path lives under `/var/lib/rancher/k3s`.

**Alternatives considered**:
- Bind compose volume — rejected.
- Copy-on-start dump/restore — out of scope.
- emptyDir — wiped on pod restart; violates daily-stop-keeps-data.

## 5. Catalog Ready only after store Ready

**Decision**: `catalogapi` has an **initContainer** that waits until `catalogdb:5432` accepts connections (`pg_isready -h catalogdb -U catalog -d catalog`). Main container: TCP or HTTP readiness on `8080`. `catalogdb`: `pg_isready`. Catalog `env` does **not** list lgtm/kafka/elasticsearch as a start gate. Optional fail-open env uses `name.namespace.svc`.

**Rationale**: FR-002 / FR-007. Same fail-open as compose (`depends_on` catalogdb only).

## 6. Compose files stay; do not run compose

**Decision**: Keep `deploy/compose.yaml` and `deploy/compose.override.yaml` unchanged. Official start **does not** run `docker compose`. Isolated tests MUST NOT run compose. README MUST NOT require Docker, Podman, or `compose up` / `compose down` / `compose build`.

**Rationale**: Clarify: k3s is the only required runnable path. Compose files stay as the identity reference.

**Alternatives considered**:
- Keep compose as a second runnable path — needs Docker/Podman; rejected.
- Delete compose files — rejected (FR-005).
- nerdctl compose on k3s containerd — extra tool; out of scope.

## 7. Layout: kustomize, no Helm, full stack

**Decision**: Official apply is `kubectl apply -f deploy/k8s/namespace.yaml && kubectl apply -k deploy/k8s`. Three overlays. No Helm. No NetworkPolicy. No second cluster.

**Rationale**: Unchanged from prior design except the runtime is k3s.

## 8. Isolated tests

**Decision**: CI/isolated tests **parse** `deploy/k8s/**` and README (every compose service image, PVC not catalog-pgdata, Catalog init wait store-only, port-forward 5000:8080, official apply `-k deploy/k8s`, k3s in Ubuntu, no Docker/Podman/k3d/minikube required, LGTM scrape targets + `compose_service` relabel + Ops/Infra mount). They MUST NOT start k3s, MUST NOT `kubectl apply`, MUST NOT run compose, MUST NOT call `k3s ctr`.

Existing `LocalK8sRulesTests` still assert minikube + `docker compose` strings. Implement **rewrites those assertions** to the k3s contracts before changing README.

## 9. Host UIs

**Decision**: Catalog host URL is `http://localhost:5000` via port-forward. Other UIs stay on the existing script (Grafana 3000, Kafka UI 8080, Kibana 5601). YAML may keep NodePort numbers as optional in-cluster hints; the documented host path is the script.

**Rationale**: Spec locked Catalog’s URL. Operators still need the same UIs.

## 10. Alloy and cadvisor on k3s

**Decision**: Alloy uses `deploy/k8s/viewer/config.alloy` (Kubernetes discovery, not compose `docker.sock`). cadvisor mounts the **k3s containerd** socket (`/run/k3s/containerd/containerd.sock`), not Docker’s `docker.sock`.

**Rationale**: No Docker on this machine.

## 11. Ordinary vs destructive stop

**Decision**:
- Ordinary: stop the port-forward process; `sudo systemctl stop k3s`. Data under `/var/lib/rancher/k3s` (including local-path PVC) **remains**.
- Destructive: with k3s running, `kubectl delete pvc catalogdb-data -n catalog` and/or wipe k3s storage. Compose `catalog-pgdata` is untouched (and unused).

Do **not** use `k3s-uninstall.sh` as the daily stop. Do **not** `kubectl delete -k deploy/k8s` as the daily stop if that overlay includes the PVC (it would wipe catalog data).

## 12. Ops/Infra live metrics on k3s (FR-016)

**Decision**: Hosting LGTM is not enough. `lgtm` MUST mount:

1. A k3s Prometheus scrape file at the same in-container path compose already uses (`/otel-lgtm/prometheus.yaml`).
2. The existing Ops/Infra dashboard (uid `eshop-ops-infra`) at the same Grafana provision path compose uses. kustomize will not load files outside `deploy/k8s/viewer/`, so keep a **byte-identical copy** at `deploy/k8s/viewer/ops-infra.json` (and `eshop-dashboards.yaml`). Isolated tests MUST fail if that copy drifts from `deploy/grafana/provisioning/dashboards/`. This is packaging, not a second dashboard.

Scrape jobs stay the compose job names (`catalogdb`, `elasticsearch`, `kafka`, `containers`) and hit **same-namespace** Services in `viewer`: `postgres-exporter:9187`, `elasticsearch-exporter:9114`, `kafka-exporter:9308`, `cadvisor:8080`. Keep LGTM self-scrape / OTLP settings (do not replace with a scrape-only file).

cadvisor on k3s does **not** emit Docker Compose labels. Prometheus `metric_relabel_configs` on job `containers` MUST copy the Kubernetes container name onto `compose_service` when it is one of: `catalogdb`, `catalogapi`, `kafka`, `connect`, `elasticsearch`, `kibana`, `kafka-ui`, `elasticsearch-exporter`, `kafka-exporter`, `postgres-exporter`. Those container names already match the compose identities. The existing Ops/Infra PromQL (`compose_service=~"catalogapi|…"`) then works. Do **not** fork `ops-infra.json`. Do **not** add a second Grafana.

Accept the board after viewer + exporters have been Ready about one minute. Every listed identity MUST appear. Ready → healthy with samples. Not Ready or stopped → not healthy. Empty / “No data” is a defect. Search MAY still be starting. Collector and Grafana stay off the board.

Isolated tests **parse** `deploy/k8s/viewer/` for the scrape file, those four targets, the `compose_service` relabel, and the Ops/Infra mount. They MUST NOT start k3s or open Grafana. Catalog Ready MUST NOT wait on this path.

**Rationale**: Clarify 2026-11-01 (FR-016 / SC-007). Today’s k3s `lgtm.yaml` is a bare image: no scrape file, no dashboard mount. Compose Prometheus also maps cadvisor’s `container_label_com_docker_compose_service` → `compose_service`. On k3s that label is missing, so Ops/Infra is empty even if exporters are up. Relabel + mounts is the smallest fix that keeps one board.

**Alternatives considered**:
- Prometheus Operator / ServiceMonitor — extra CRDs; rejected (no Helm, simplicity).
- kube-prometheus-stack — second Grafana; rejected (FR-014).
- Rewrite `ops-infra.json` to k8s `pod` labels — second dashboard fork; rejected.
- Mount compose `deploy/prometheus/prometheus.yaml` unchanged — exporter short names would work in `viewer`, but cadvisor still has no `compose_service`.
- Edit compose files — out of scope (FR-005).
