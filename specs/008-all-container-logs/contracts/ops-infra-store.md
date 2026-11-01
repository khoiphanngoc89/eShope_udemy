# Contract: Ops/Infra store detail

Store PromQL for click-through detail. The board contract is [ops-infra-overview.md](./ops-infra-overview.md).

When Ops/Infra `service=catalogdb`:

```promql
pg_up
pg_stat_database_numbackends{datname="catalog"}
pg_database_size_bytes{datname="catalog"}
```

Small or zero size is not not-healthy. No `stat_statements` / SQL text. Catalog does not scrape the store.
