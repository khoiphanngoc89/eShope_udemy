# Contract: k3s Catalog (P1 increment)

Operator-facing contract. No new Catalog HTTP route. JSON bodies stay [004-local-dev-stack/contracts/catalog-api.md](../../004-local-dev-stack/contracts/catalog-api.md).

## Apply (increment)

Catalog overlay can be applied alone while writing YAML. **Documented start** is the full stack in [k3s-search.md](./k3s-search.md) (`kubectl apply -k deploy/k8s`).

```bash
kubectl apply -f deploy/k8s/namespace.yaml
kubectl apply -k deploy/k8s/catalog
```

Must create:

| Kind | Name | Namespace |
|------|------|-----------|
| Namespace | `catalog` | (and `search`, `viewer` in the same `namespace.yaml`) |
| PersistentVolumeClaim | `catalogdb-data` | `catalog` |
| Deployment + Service | `catalogdb` | `catalog` (`postgres:16.15`) |
| Deployment + Service | `catalogapi` | `catalog` (`eshop-catalog-api:local`, `imagePullPolicy: Never`) |

## Ready

- `catalogdb` Ready only after `pg_isready` succeeds.
- `catalogapi` pods MUST NOT reach Ready until the store accepts connections (initContainer or equivalent).
- Catalog MUST NOT list Grafana / Loki / Tempo / Kafka / Elasticsearch as a start gate.
- Store connection uses same-namespace host `catalogdb`. Optional env for other namespaces MUST use `name.namespace.svc` and MUST NOT be a Ready gate.

## Host URL (same as compose used)

```bash
./deploy/k8s/port-forward-localhost.sh
```

Then:

- `GET http://localhost:5000/health` → 200 (or existing Catalog health)
- `POST http://localhost:5000/api/v1/products` + `GET .../api/v1/products/{id}` per 004 contract

`catalogapi` Service MUST NOT be NodePort 5000. Host port 5000 exists only via port-forward.

## Image (no Docker)

```bash
dotnet publish src/Services/eShop.Catalog.Api/eShop.Catalog.Api.csproj \
  -c Release \
  /t:PublishContainer \
  /p:ContainerRepository=eshop-catalog-api \
  /p:ContainerImageTag=local \
  /p:ContainerArchiveOutputPath=./eshop-catalog-api-local.tar
sudo k3s ctr images import ./eshop-catalog-api-local.tar
```

MUST NOT require `docker`, `podman`, or `docker compose`.

## Isolated tests

File-parse `deploy/k8s/catalog/**` + README. MUST NOT start k3s or `kubectl apply`.
