# Contract: Loki Explore (collector path)

Operator contract. Not a Catalog HTTP API. `dotnet test` does not start Grafana or the collector.

Auth: local Grafana **admin** / **admin**. Loki data source already provisioned (uid `loki`). Do not add one.

## 1. Data source (FR-001)

After `lgtm` is up, Explore → Loki works without a new data source.

## 2. Catalog lines (FR-002, SC-001)

With `catalogapi`, `log-collector`, and `lgtm` up, after at least one Catalog request, last 15 minutes:

```logql
{compose_service="catalogapi"}
```

**Success**: at least one line from Catalog container stdout.

**Empty (valid)**: no Catalog traffic yet — empty result, not a data-source error.

## 3. Kafka broker process lines (FR-002, SC-001)

With `kafka`, `log-collector`, and `lgtm` up, after the broker is healthy, last 15 minutes:

```logql
{compose_service="kafka"}
```

**Success**: broker process lines. Query does not return Catalog lines.

**Failure of the feature**: Loki shows topic JSON payloads as the primary result, or the operator must open Kafka UI to see *process* logs.

## 4. Catalog errors (FR-003, SC-002)

After one failed Catalog request that logs Error (name, duration, stack):

```logql
{compose_service="catalogapi"} |= "Error"
```

or `|= "failed"` if that is the console wording.

**Success**: that error text is visible within 2 minutes. Existing OTLP selector `{service_name=~".*eShop.Catalog.Api.*"}` MAY still show the same event (overlap allowed).

## 5. Fail-open (FR-005)

Catalog create / get-by-id succeed if `log-collector` or `lgtm` is stopped. Kafka broker stays up.
