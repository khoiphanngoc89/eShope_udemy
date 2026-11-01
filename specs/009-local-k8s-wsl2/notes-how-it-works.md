# Notes: How local Kubernetes works, and how we implement it

**Feature**: [spec.md](./spec.md)  
**Audience**: developer on Windows + Ubuntu WSL2 (no Docker)  
**Not a substitute for** `/speckit-plan` — classroom + implementation sketch after clarify.

Decided in clarify (2026-11-01):

| Decision | Choice |
|----------|--------|
| Official local cluster | **k3s in Ubuntu on WSL2** (containerd) |
| Catalog data | **Separate** from compose `catalog-pgdata` |
| Catalog URL | **Same as compose used**: `http://localhost:5000` |
| Compose | **Files stay.** Running compose is not required. |
| Which services on k3s | **All compose identities** |
| How many clusters | **One** k3s, **three namespaces** (`catalog`, `search`, `viewer`) |
| Not required | Docker, Podman, k3d, kind, minikube, Docker Desktop Kubernetes |
| Grafana Ops/Infra | Live metrics after viewer + exporters Ready. Empty panels are a defect. Same board as compose (`compose_service` via cadvisor relabel). |

Isolated `dotnet test` does not start k3s.

---

## 1. How a cluster works (short)

Compose (reference only): a compose file lists containers. Kubernetes: you describe **desired state**. The cluster keeps trying to make it true.

```
You (kubectl apply)
        │
        ▼
API server
        │
        ▼
Scheduler places work on a node
        │
        ▼
Node runs Pods
```

| Word | Meaning here |
|------|----------------|
| **Cluster** | k3s in Ubuntu (API + one node) |
| **Node** | This Ubuntu WSL2 distro |
| **Pod** | Smallest running unit |
| **Deployment** | Keep N copies of a Pod running |
| **Service** | Stable DNS name + port inside the cluster |
| **Probe** | Health check. Catalog is not Ready until the store is Ready |
| **Volume / PVC** | Disk for Postgres inside k3s. Not the compose volume |
| **kubectl** | `k3s kubectl` from WSL2 |

**Inside vs outside:**

- Inside: Catalog → `Host=catalogdb;Port=5432`
- On your machine: `http://localhost:5000` via `deploy/k8s/port-forward-localhost.sh`

---

## 2. How k3s fits on your machine

```
Windows
  └── WSL2 Ubuntu
        ├── k3s (containerd)     ← official path
        │     ├── ns catalog     catalogdb, catalogapi
        │     ├── ns search      kafka, connect, es, …
        │     └── ns viewer      lgtm, alloy, exporters
        └── compose files        ← reference only; do not run
```

k3s install disables Traefik and ServiceLB. Host ports come from the port-forward script. PVC uses k3s `local-path`.

---

## 3. Image without Docker

Public images are pulled by k3s. Catalog is:

1. `dotnet publish … -t:PublishContainer` → `eshop-catalog-api-local.tar` (Git Bash: not `/t:`)
2. `sudo k3s ctr images import …` (or `wsl.exe -d Ubuntu -u root` if `sudo` waits for a password)
3. Deployment `image: eshop-catalog-api:local`, `imagePullPolicy: Never`
4. After a same-tag import: `kubectl -n catalog rollout restart deploy/catalogapi`

---

## 4. Start / stop

| Action | Command |
|--------|---------|
| Start k3s | `sudo systemctl start k3s` |
| Apply | `kubectl apply -f deploy/k8s/namespace.yaml && kubectl apply -k deploy/k8s` |
| Host URL | `./deploy/k8s/port-forward-localhost.sh` |
| Ordinary stop | stop the script; `sudo systemctl stop k3s` (PVC kept) |
| Destructive | `kubectl delete pvc catalogdb-data -n catalog` |

Do not `docker compose up` / `down`. Do not uninstall k3s as the daily stop.

---

## 5. Out of scope

- Cloud Kubernetes, TLS, Helm, NetworkPolicy
- Running compose on this laptop
- Sharing `catalog-pgdata` into k3s
- A second local cluster product
