# Contract: Loki Explore (all-container path)

Operator contract. Not a Catalog HTTP API. `dotnet test` does not start Grafana or the collector.

Auth: local Grafana **admin** / **admin**. Loki data source already provisioned (uid `loki`). Do not add one.

## 1. Data source (FR-001)

After `lgtm` is up, Explore → Loki works without a new data source. Process-line search stays here (not on the Ops/Infra health overview).

## 2. Non-application process lines (FR-002, SC-001)

With `log-collector` and `lgtm` up, after each target is healthy and has written process output, last 15 minutes:

```logql
{compose_service="catalogdb"}
{compose_service="kafka"}
{compose_service="connect"}
{compose_service="elasticsearch"}
{compose_service="kibana"}
{compose_service="kafka-ui"}
```

Exporters (`elasticsearch-exporter`, `kafka-exporter`, `postgres-exporter`, `cadvisor`) and `connect-register` use the same `{compose_service="<name>"}` form when those containers are started.

**Success**: matching process lines for that identity.

**Empty (valid)**: container has not written yet — empty result, not a data-source or connection error (SC-005).

**Failure of the feature**: operator must add a Loki data source; Kafka query returns topic JSON as the primary result.

## 3. Catalog application lines once (FR-003, SC-002)

After at least one Catalog public request with application export on, last 15 minutes:

```logql
{service_name=~".*eShop.Catalog.Api.*"}
```

**Success**: those application lines appear from the application path. The same request text is **not** also listed from `{compose_service="catalogapi"}`.

Catalog errors keep today’s usefulness:

```logql
{service_name=~".*eShop.Catalog.Api.*"} |= "Error"
```

(or `|= "failed"` if that is the console wording).

**Failure of the feature**: the same Catalog request line appears twice (OTLP + container tail).

## 4. Topic payloads stay in Kafka UI (FR-004)

`{compose_service="kafka"}` is broker process text. Topic bodies stay at Kafka UI http://localhost:8080.

## 5. Fail-open (FR-005)

Catalog create / get-by-id succeed if `log-collector` or `lgtm` is stopped.
