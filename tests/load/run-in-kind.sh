#!/usr/bin/env bash
# Runs transfers.js from a pod inside the kind cluster deployed by deploy/k8s/kind.sh.
# Usage: RATE=25 DURATION=60s tests/load/run-in-kind.sh
set -euo pipefail
cd "$(dirname "$0")"
NS=ledgerline
# For the duration of the run, restored afterwards:
#  - all virtual users share one source IP: lift the gateway's per-IP rate limit;
#  - two accounts paying each other 25 times a second would trip the velocity rule and park everything in review:
#    lift it, so that every transfer runs the whole saga (the rule itself is covered by unit tests).
kubectl -n "$NS" set env deployment/gateway RateLimiting__PermitPerMinute=1000000 >/dev/null
kubectl -n "$NS" set env deployment/fraud Fraud__Policy__VelocityLimit=1000000 >/dev/null
trap 'kubectl -n "$NS" set env deployment/gateway RateLimiting__PermitPerMinute- >/dev/null; kubectl -n "$NS" set env deployment/fraud Fraud__Policy__VelocityLimit- >/dev/null' EXIT
kubectl -n "$NS" rollout status deployment/gateway --timeout=180s >/dev/null
kubectl -n "$NS" rollout status deployment/fraud --timeout=180s >/dev/null
kubectl -n "$NS" create configmap k6-script --from-file=transfers.js --dry-run=client -o yaml | kubectl apply -f - >/dev/null
kubectl -n "$NS" delete pod k6 --ignore-not-found >/dev/null
kubectl -n "$NS" run k6 --restart=Never --image=grafana/k6:latest --overrides "$(cat <<JSON
{
  "spec": {
    "securityContext": { "runAsNonRoot": true, "runAsUser": 12345, "seccompProfile": { "type": "RuntimeDefault" } },
    "containers": [{
      "name": "k6",
      "image": "grafana/k6:latest",
      "args": ["run", "--env", "RATE=${RATE:-25}", "--env", "DURATION=${DURATION:-60s}", "/scripts/transfers.js"],
      "securityContext": { "allowPrivilegeEscalation": false, "capabilities": { "drop": ["ALL"] } },
      "volumeMounts": [{ "name": "script", "mountPath": "/scripts" }]
    }],
    "volumes": [{ "name": "script", "configMap": { "name": "k6-script" } }]
  }
}
JSON
)" >/dev/null
kubectl -n "$NS" wait --for=condition=Ready pod/k6 --timeout=120s >/dev/null || true
kubectl -n "$NS" logs -f k6
kubectl -n "$NS" wait --for=jsonpath='{.status.phase}'=Succeeded pod/k6 --timeout=30s >/dev/null
