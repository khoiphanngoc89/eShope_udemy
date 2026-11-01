# Contract: Log/App (Elasticsearch data source)

Operator contract. Not a Catalog HTTP API. `dotnet test` does not start Grafana.

Auth: local Grafana **admin** / **admin**. Elasticsearch local-dev: no auth.

## 1. Data source (FR-001, FR-005)

Provisioned Elasticsearch data source in Grafana:

| Setting | Value |
|---------|--------|
| Uid | `elasticsearch` |
| URL | in-stack `http://elasticsearch:9200` |
| Index | `catalog-app-logs*` |
| Time field | `@timestamp` |
| Auth | none |

**Success**

- Log/App dashboard opens without adding a data source by hand.
- Empty index / no errors: empty result, not a configuration error.

## 2. Error document (FR-005, FR-006, SC-003)

After one failed Catalog request that logs Error (name, duration, stack), a document appears in `catalog-app-logs` within 2 minutes and is visible on Log/App.

Required fields: `@timestamp`, `message`, `level`, `service.name` = `eShop.Catalog.Api`. Exception/stack when the failure had one.

**Success**

- Filter to Catalog shows that error text.
- Loki Explore still works for the same request (not removed).

**Failure of the feature**

- Operator must create the Elasticsearch data source in the Grafana UI.
- Failed-request Error never appears in `catalog-app-logs` while the sink is configured and Elasticsearch is up.
- Isolated `dotnet test` requires Elasticsearch.

## 3. Fail-open (FR-007)

If Elasticsearch or the Serilog ES sink cannot write, Catalog still starts. Console and Loki still receive the Error. Create / get-by-id still succeed.
