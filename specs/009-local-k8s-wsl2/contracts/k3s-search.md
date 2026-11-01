# Contract: k3s full stack (three namespaces)

One k3s cluster. Official documented start applies **all** compose identities into `catalog`, `search`, and `viewer`. Catalog Ready and `http://localhost:5000` create/get-by-id MUST NOT wait on `search` or `viewer`.

## Official apply

```bash
kubectl apply -f deploy/k8s/namespace.yaml
kubectl apply -k deploy/k8s
```

`namespace.yaml` MUST create Namespaces `catalog`, `search`, and `viewer`. Root `deploy/k8s/kustomization.yaml` MUST include `catalog/`, `search/`, and `viewer/`.

## Search overlay (`deploy/k8s/search/`)

Namespace **`search`**. Same images as `deploy/compose.yaml`:

| Name | Image | Cross-ns notes |
|------|-------|----------------|
| `kafka` | `apache/kafka:3.8.1` | From `catalog`: `kafka.search.svc:9092` |
| `connect` | `quay.io/debezium/connect:2.7.3.Final` | Store: `catalogdb.catalog.svc`. MUST NOT become Ready until `public.mt_doc_product` exists |
| `connect-register` | `quay.io/debezium/connect:2.7.3.Final` | Catalog: `catalogapi.catalog.svc`. MUST wait until `public.mt_doc_product` exists, then POST the connector |
| `elasticsearch` | `docker.elastic.co/elasticsearch/elasticsearch:8.15.3` | From `catalog`: `elasticsearch.search.svc:9200` |
| `kibana` | `docker.elastic.co/kibana/kibana:8.15.3` | |
| `kafka-ui` | `provectuslabs/kafka-ui:v0.7.2` | |

Catalog Deployment MUST become Ready if these pods are down.

## Viewer overlay (`deploy/k8s/viewer/`)

Namespace **`viewer`**. Same images as compose:

| Name | Image | Cross-ns notes |
|------|-------|----------------|
| `lgtm` | `grafana/otel-lgtm:0.38.0` | From `catalog`: `lgtm.viewer.svc:4318` |
| `log-collector` | `grafana/alloy:v1.21.3` | Discover `catalog`, `search`, `viewer` |
| `postgres-exporter` | `quay.io/prometheuscommunity/postgres-exporter:v0.17.1` | `catalogdb.catalog.svc` |
| `elasticsearch-exporter` | `quay.io/prometheuscommunity/elasticsearch-exporter:v1.8.0` | `elasticsearch.search.svc` |
| `kafka-exporter` | `danielqsj/kafka-exporter:v1.11.0` | `kafka.search.svc` |
| `cadvisor` | `ghcr.io/google/cadvisor:v0.60.6` | k3s containerd socket |

- At most one local Grafana/LGTM-style viewer (no second product).
- Alloy uses `deploy/k8s/viewer/config.alloy` (k8s discovery, not compose `docker.sock`).
- Catalog MUST NOT wait on OTLP / Grafana / Alloy / exporters to become Ready.
- `lgtm` MUST mount a Prometheus scrape file and the existing Ops/Infra dashboard. Hosting the viewer without live metrics fails [k3s-ops-infra.md](./k3s-ops-infra.md) (FR-016).

## Host UIs

| Host | How to reach it |
|------|-----------------|
| Catalog (required) | `./deploy/k8s/port-forward-localhost.sh` → `http://localhost:5000` |
| Grafana | same script → `http://localhost:3000` |
| Kafka UI | same script → `http://localhost:8080` |
| Kibana | same script → `http://localhost:5601` |

## Isolated tests

File-parse MUST assert three namespaces, root kustomization lists all three overlays, cross-ns hostnames where calls leave a namespace, and Catalog start-gate files do not wait on `search` / `viewer`. Tests MUST NOT start k3s.
