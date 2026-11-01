# Contract: Compose files stay (not a run path)

## Files

Keep these files in the repo. This feature MUST NOT delete or rewrite them:

- `deploy/compose.yaml`
- `deploy/compose.override.yaml`

They remain the identity list for what k3s must host.

## Do not run compose

Official k3s start MUST NOT require:

- `docker compose up`
- `docker compose down`
- `docker compose build`
- Docker, Podman, or Docker Desktop

Isolated tests MUST NOT run compose.

## Cluster store vs compose store

| Store | Volume / PVC | When it is used |
|-------|----------------|-----------------|
| Compose | volume `catalog-pgdata` | unused by this feature |
| Cluster | PVC `catalogdb-data` | k3s Catalog |

They MUST stay separate. Cluster manifests MUST NOT mount `catalog-pgdata`.

## Isolated tests

README MUST say compose files stay and that running compose is not required. README MUST NOT document `docker compose` as a start or stop step for the k3s path.
