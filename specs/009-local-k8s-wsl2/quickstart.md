# Quickstart: Local k3s on WSL2

Operator validation after implement. Isolated `dotnet test` does **not** run these cluster steps.

Prerequisites: Ubuntu WSL2 with systemd, k3s (`--disable traefik --disable servicelb`), .NET SDK, kubectl (`k3s kubectl`). No Docker. No Podman. See [notes-how-it-works.md](./notes-how-it-works.md). Contracts: [k3s-catalog.md](./contracts/k3s-catalog.md), [compose-files.md](./contracts/compose-files.md), [k3s-ops-infra.md](./contracts/k3s-ops-infra.md).

## 1. Isolated tests (no cluster)

From repo root (Windows or WSL2):

```bash
dotnet test tests/Services/eShop.Catalog.Api.Tests/eShop.Catalog.Api.Tests.csproj --filter FullyQualifiedName~LocalK8s
```

Expect: pass. k3s is not started.

## 2. Catalog image (no Docker)

From WSL2 at repo root:

```bash
dotnet publish src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj \
  -c Release \
  /t:PublishContainer \
  /p:ContainerRepository=eshop-catalog-api \
  /p:ContainerImageTag=local \
  /p:ContainerArchiveOutputPath=./eshop-catalog-api-local.tar
sudo k3s ctr images import ./eshop-catalog-api-local.tar
```

Expect: image `eshop-catalog-api:local` listed by `sudo k3s ctr images ls`.

## 3. Apply the full stack

```bash
sudo systemctl start k3s
kubectl apply -f deploy/k8s/namespace.yaml
kubectl apply -k deploy/k8s
kubectl -n catalog rollout status deploy/catalogdb
kubectl -n catalog rollout status deploy/catalogapi
```

Expect: Catalog and store rollouts succeed even if search/viewer pods are still starting. Catalog pod Ready only after store Ready.

## 4. Same Catalog URL compose used

```bash
./deploy/k8s/port-forward-localhost.sh
```

Other terminal:

```bash
curl -sS -o /dev/null -w "%{http_code}\n" http://localhost:5000/health
```

Expect: `200`. Then POST + GET-by-id a product (004 contract). If Windows `localhost` fails, curl from WSL2.

## 5. Ops/Infra live metrics

Keep the port-forward running. Wait about one minute after `lgtm` and the metric exporters are Ready (search MAY still be starting):

```bash
kubectl -n viewer rollout status deploy/lgtm
kubectl -n viewer rollout status deploy/postgres-exporter
kubectl -n viewer rollout status deploy/elasticsearch-exporter
kubectl -n viewer rollout status deploy/kafka-exporter
kubectl -n viewer rollout status deploy/cadvisor
```

Open `http://localhost:3000/d/eshop-ops-infra/ops-infra` (admin / admin). Last 15 minutes.

Expect: store, Catalog.Api, Kafka, connector, search index, Kibana, Kafka UI, and metric exporters all appear. Ready ones healthy with samples. Not-ready or stopped ones not healthy. Empty / “No data” on those panels fails. Collector and Grafana need not appear. Click a Ready container for detail.

Windows `localhost` may need `127.0.0.1` (IPv6 `localhost` is not forwarded).

## 6. Fail-open

With search or viewer pods deleted (or not Ready): Catalog health and create/get-by-id still succeed. After search-sync is Ready and a product has been synced, browse on the same Catalog URL returns items. Ops/Infra may show those deleted identities as not healthy; empty panels still fail.

## 7. Ordinary stop

Stop the port-forward process. Then `sudo systemctl stop k3s`. PVC `catalogdb-data` remains. Compose files remain. Compose is not started.

## Destructive (only when you want an empty cluster store)

With k3s running: `kubectl delete pvc catalogdb-data -n catalog`. Do **not** delete `deploy/compose.yaml`.
