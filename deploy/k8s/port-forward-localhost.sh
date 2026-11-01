#!/usr/bin/env bash
# Forward cluster Services to the same localhost ports as compose.
# Run from WSL2 after the stack is Ready. Ctrl+C stops every forward.
set -euo pipefail

if ! command -v kubectl >/dev/null 2>&1; then
  kubectl() {
    sudo k3s kubectl "$@"
  }
fi

ADDRESS="${ADDRESS:-0.0.0.0}"

cleanup() {
  jobs -pr | xargs -r kill 2>/dev/null || true
}
trap cleanup EXIT INT TERM

echo "forwarding to ${ADDRESS} (Ctrl+C stops all)"

kubectl -n catalog port-forward svc/catalogapi 5000:8080 --address "${ADDRESS}" &
kubectl -n catalog port-forward svc/catalogdb 5432:5432 --address "${ADDRESS}" &
kubectl -n search port-forward svc/kafka 9092:9092 --address "${ADDRESS}" &
kubectl -n search port-forward svc/connect 8083:8083 --address "${ADDRESS}" &
kubectl -n search port-forward svc/elasticsearch 9200:9200 --address "${ADDRESS}" &
kubectl -n search port-forward svc/kibana 5601:5601 --address "${ADDRESS}" &
kubectl -n search port-forward svc/kafka-ui 8080:8080 --address "${ADDRESS}" &
kubectl -n viewer port-forward svc/lgtm 3000:3000 4317:4317 4318:4318 --address "${ADDRESS}" &
kubectl -n viewer port-forward svc/postgres-exporter 9187:9187 --address "${ADDRESS}" &
kubectl -n viewer port-forward svc/elasticsearch-exporter 9114:9114 --address "${ADDRESS}" &
kubectl -n viewer port-forward svc/kafka-exporter 9308:9308 --address "${ADDRESS}" &
kubectl -n viewer port-forward svc/cadvisor 8081:8080 --address "${ADDRESS}" &

cat <<'EOF'

localhost (same ports as compose):
  Catalog          http://localhost:5000
  Grafana          http://localhost:3000   (admin / admin)
  Kafka UI         http://localhost:8080
  Kibana           http://localhost:5601
  Connect          http://localhost:8083
  Elasticsearch    http://localhost:9200
  Postgres         localhost:5432
  Kafka            localhost:9092
  postgres-exp     http://localhost:9187/metrics
  es-exporter      http://localhost:9114/metrics
  kafka-exporter   http://localhost:9308/metrics
  cadvisor         http://localhost:8081
EOF

wait
