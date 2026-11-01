# Contract: Catalog search sync

Operator and compose contract for change-data capture. Not a Catalog HTTP API. `dotnet test` does not start this stack.

Auth: none on local Kafka UI and local Kibana (local-dev defaults).

## 1. Topic prefix (FR-029, FR-030)

Every catalog search-sync topic name starts with `eshop.catalog.sync`.

| Topic | Purpose |
|-------|---------|
| `eshop.catalog.sync.public.mt_doc_product` | Product snapshot (`op=r`) and stream (`op=c`/`u`/`d`) |
| `eshop.catalog.sync.connect-configs` | Kafka Connect configs |
| `eshop.catalog.sync.connect-offsets` | Kafka Connect offsets |
| `eshop.catalog.sync.connect-status` | Kafka Connect status |
| `eshop.catalog.sync.schema-history` | Debezium schema history |

**Success**

- Kafka UI filter `eshop.catalog.sync` lists every catalog search-sync topic
- No catalog search-sync topic uses another prefix

## 2. Snapshot then stream (FR-027, FR-028)

Debezium PostgreSQL connector, `snapshot.mode=initial`, table `public.mt_doc_product`, `wal_level=logical` on `catalogdb`.

**Success**

- Existing active, not-deleted products appear in Elasticsearch `catalog-products` after snapshot without an update
- A later create or update is searchable within 5 seconds under normal sync
- Create/update HTTP success does not wait for the index

## 3. Kafka UI

Documented local HTTP URL on the compose published port. Cluster bootstrap is the in-stack Kafka host.

**Success**

- Operator can open Kafka UI and filter `eshop.catalog.sync`
- Catalog.Api starts and accepts create-product / get-by-id if Kafka UI is stopped

## 4. Kibana (FR-033, SC-020)

Documented local HTTP URL: host port **5601**. Kibana uses in-stack `http://elasticsearch:9200`. Index pattern / data view: `catalog-products`.

**Success**

- After at least one product is indexed, operator opens Kibana and sees that document
- Catalog.Api, create-product, get-by-id, and `dotnet test` succeed with Kibana stopped

**Failure of the feature**

- Kibana missing from the compose pair
- Operator cannot reach Kibana on the documented local URL while elasticsearch is up
- Catalog refuses to start because Kibana is down
