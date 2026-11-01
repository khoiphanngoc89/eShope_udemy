# Review notes: Modern Grafana log collection (how it works + how to implement)

Date: 2026-11-01  
Spec: [007-loki-promtail](./spec.md) · Plan: [plan.md](./plan.md)  
Grafana: http://localhost:3000 (admin / admin)

**Status:** Specified, clarified, and planned. Collector is **not in compose yet**. This note is how it will work and how to implement it.

---

# Part A — How it works

One Grafana (`lgtm`). Loki is already inside it (002). This feature adds a **Grafana-family collector** that ships **container process logs** for Catalog and the Kafka **broker** into that same Loki.

```
catalogapi stdout ──┐
                    ├──► collector (Grafana Alloy; Promtail fallback)
kafka stdout ───────┘              │
                                   ▼
                              Loki (inside lgtm)
                                   │
                                   ▼
                         Grafana Explore → Loki
                    Catalog lines  |  Kafka broker process lines
```

This is **not** Kafka topic contents. Message bodies stay in Kafka UI (http://localhost:8080).

## A1. What you see where

| You want | Open | Not here |
|----------|------|----------|
| Catalog process / request logs (collector path) | Explore → **Loki** → `{compose_service="catalogapi"}` | Ops/Infra, Kibana |
| Catalog **Error** text (006 hybrid) | Dashboard **Log/App** (Elasticsearch `catalog-app-logs`) | Kafka UI |
| Kafka **broker process** logs | Explore → **Loki** → `{compose_service="kafka"}` | Kafka UI topics |
| Kafka **topic payloads** (CDC product JSON) | Kafka UI http://localhost:8080 topics `eshop.catalog.sync*` | Loki |
| Search-index CPU / heap / red | Dashboard **Ops/Infra** (Prometheus) | Loki |
| Product search documents | Kibana `catalog-products` | Loki |

006 hybrid stays. 002 Loki Explore stays. This feature does **not** replace either.

## A2. Two paths Catalog logs can take

Catalog already writes console + OTLP → LGTM → Loki (001/002). After 007, a collector **also** tails Catalog **container stdout** and pushes to Loki.

| Path | Needs Catalog code change? | Survives if OTLP is down? |
|------|----------------------------|---------------------------|
| Serilog → OTLP `:4318` | No (already there) | No |
| Collector → Loki push | No (stdout only) | Yes, if the container still prints |

Operators may see **overlap** (same request twice, different labels). Spec allows that. Do not remove OTLP in this slice.

| Path | How you find Catalog |
|------|----------------------|
| OTLP (today) | `{service_name=~".*eShop.Catalog.Api.*"}` |
| Collector (007) | `{compose_service="catalogapi"}` |

## A3. Kafka in Grafana — process logs only

Clarify session 2026-11-01: **Catalog + Kafka broker process logs**. Not Connect. Not Kafka UI. Not topic bodies.

| Source | In Loki after 007? |
|--------|--------------------|
| `eshop_kafka` stdout/stderr | Yes |
| `eshop_catalog_api` stdout/stderr | Yes |
| `eshop_debezium_connect` | No |
| `eshop_kafka_ui` | No |
| Topic `eshop.catalog.sync.public.mt_doc_product` messages | No — Kafka UI |

Kafka broker lines look like listener bind, controller, errors. They are **not** the product JSON Debezium puts on the topic. Filter Kafka separately from Catalog.

## A4. Fail-open

| Down | Catalog create / get-by-id | Kafka broker | Isolated `dotnet test` |
|------|----------------------------|--------------|------------------------|
| Collector stopped | Works | Keeps running | Pass (no Docker) |
| Grafana / Loki stopped | Works | Keeps running | Pass |
| Collector up, no Catalog traffic yet | — | Broker lines still appear | — |

`catalogapi` must **not** `depends_on` the collector or `lgtm`. Kafka must **not** `depends_on` the collector.

## A5. Mental model

- **Loki + collector** = Grafana-native “what did the process print?”
- **Elasticsearch Log/App** = structured Catalog Error documents (006)
- **Ops/Infra** = is the search-index cluster broken? (Prometheus)
- **Kafka UI** = what is **on the topic**
- Empty Loki result = no lines yet, not a missing data source

---

# Part B — How to implement (detail)

Rules: one compose pair, one Grafana (`lgtm`), Catalog and Kafka start without the collector, isolated `dotnet test` does not start Docker, no new Catalog HTTP route, do not ship topic payloads.

## B0. Order of work

```
1. Pin Grafana Alloy (one service)
2. Confirm Loki is reachable at http://lgtm:3100 from the eshop network
3. Add log-collector to compose.yaml + override (Catalog/Kafka must not depend on it)
4. Keep only eshop_catalog_api + eshop_kafka
5. Isolated tests (depends_on + keep-list)
6. README LogQL
7. Start and check Explore → Loki
```

Do **not** add a second Grafana or compose file. Do **not** edit `BuildBlock.Logging.Tests`. Do **not** remove 002 OTLP or 006 dashboards.

## B1. Collector image

**Decision (plan):** `grafana/alloy` pinned v1.x. Service name `log-collector`. Promtail is the fallback if Alloy is blocked. Do not run both.

## B2. Loki push URL

Phase 0 already proved `http://lgtm:3100/ready` from network `eshop`. Collector write URL:

```text
http://lgtm:3100/loki/api/v1/push
```

If a future LGTM binds Loki to `127.0.0.1` only, publish `3100:3100` on `lgtm`. Do not add a second Loki. No auth.

Re-check:

```bash
docker run --rm --network eshop curlimages/curl:8.10.1 -sf http://lgtm:3100/ready
```

## B3. Compose

`deploy/compose.yaml`:

```yaml
log-collector:
  image: grafana/alloy:<pinned>
```

`deploy/compose.override.yaml`:

```yaml
log-collector:
  container_name: eshop_log_collector
  command: ["run", "/etc/alloy/config.alloy", "--storage.path=/var/lib/alloy/data"]
  volumes:
    - ./alloy/config.alloy:/etc/alloy/config.alloy:ro
    - /var/run/docker.sock:/var/run/docker.sock:ro
  depends_on:
    lgtm:
      condition: service_healthy
  restart: unless-stopped
```

`depends_on: lgtm` is OK for the **collector** only.

`catalogapi` stays:

```yaml
depends_on:
  catalogdb:
    condition: service_healthy
```

`kafka` must not wait on `log-collector`. Windows Docker Desktop: `/var/run/docker.sock` still works.

## B4. Keep only two containers

| Compose service | `container_name` | Loki label |
|-----------------|------------------|------------|
| `catalogapi` | `eshop_catalog_api` | `compose_service=catalogapi` |
| `kafka` | `eshop_kafka` | `compose_service=kafka` |

Alloy: `discovery.docker` + relabel keep those two names + `loki.source.docker` + `loki.write` to `lgtm:3100`.

Promtail fallback:

```yaml
scrape_configs:
  - job_name: docker
    docker_sd_configs:
      - host: unix:///var/run/docker.sock
        refresh_interval: 5s
    relabel_configs:
      - source_labels: ['__meta_docker_container_name']
        regex: '/(eshop_catalog_api|eshop_kafka)'
        action: keep
      - source_labels: ['__meta_docker_container_label_com_docker_compose_service']
        target_label: compose_service
    pipeline_stages:
      - docker: {}
clients:
  - url: http://lgtm:3100/loki/api/v1/push
```

Do **not** subscribe to Kafka topics. Do **not** tail Kafka data-dir segment files.

## B5. Isolated tests (no Docker)

Same style as 006 `HybridObservabilityFailOpenTests`:

1. `catalogapi.depends_on` contains `catalogdb`, not `log-collector` / `lgtm`.
2. `kafka` does not `depends_on` the collector.
3. Collector config text mentions `eshop_catalog_api` and `eshop_kafka` only as keep targets.
4. Create / get-by-id on `CatalogApiFactory` still pass.

## B6. Operator queries

```logql
{compose_service="catalogapi"}
{compose_service="catalogapi"} |= "Error"
{compose_service="kafka"}
{service_name=~".*eShop.Catalog.Api.*"}   # existing OTLP path
```

```bash
docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d --wait \
  lgtm kafka catalogapi log-collector
```

Kafka UI for topic bodies: http://localhost:8080.

## B7. Verify

1. Explore → Loki, last 15 minutes.
2. `{compose_service="kafka"}` after broker healthy.
3. One Catalog request → `{compose_service="catalogapi"}`.
4. Each filter shows only that service.
5. `stop log-collector` → create 201, get-by-id 200; Kafka still up.

If empty: wait ~1 minute; check collector cannot connect to `lgtm:3100`; check `docker ps --format "{{.Names}}"`.

## B8. File map (after implement)

| File | Role |
|------|------|
| `deploy/compose.yaml` | `log-collector` image |
| `deploy/compose.override.yaml` | Socket mount; no Catalog/Kafka `depends_on` collector |
| `deploy/alloy/config.alloy` | Keep two names; push `lgtm:3100` |
| `README.md` | Start + LogQL Catalog vs Kafka |
| Catalog tests | `depends_on` + fail-open |

No Catalog C# for shipping. Do not add a Loki Serilog sink (002). Do not add ES sink here (006).

## B9. Decisions (do not undo)

| Decision | Why |
|----------|-----|
| One Alloy collector, existing Loki | Option 1; FR-007 |
| Docker socket discovery | Process logs without app code |
| Only `catalogapi` + `kafka` | Clarify 2026-11-01 |
| No topic consume | Kafka UI stays the topic viewer |
| Catalog/Kafka do not wait on collector | FR-005 |
| Keep OTLP and 006 | FR-008 |

Related: [006 review notes](../006-hybrid-observability/review-notes.md). Implement 007 after `/speckit-tasks`.
