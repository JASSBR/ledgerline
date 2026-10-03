#!/usr/bin/env bash
# Builds the four images, loads them into a kind cluster and deploys the bank, then smoke-tests it end to end:
# health of every pod, a Keycloak token, an authenticated call through the gateway into the Ledger.
# Usage: deploy/k8s/kind.sh   (needs docker, kind, kubectl; creates the cluster "ledgerline" if missing)
set -euo pipefail
cd "$(dirname "$0")/../.."

CLUSTER=ledgerline
NS=ledgerline
PROJECTS="ledger=src/Services/Ledger/Ledgerline.Ledger/Ledgerline.Ledger.csproj
payments=src/Services/Payments/Ledgerline.Payments/Ledgerline.Payments.csproj
fraud=src/Services/Fraud/Ledgerline.Fraud/Ledgerline.Fraud.csproj
gateway=src/Ledgerline.Gateway/Ledgerline.Gateway.csproj"

kind get clusters | grep -qx "$CLUSTER" || kind create cluster --name "$CLUSTER" --wait 120s

for entry in $PROJECTS; do
  name=${entry%%=*}
  echo "→ image ledgerline-$name"
  docker build -q --build-arg PROJECT="${entry#*=}" -t "ledgerline-$name:local" . >/dev/null
  kind load docker-image "ledgerline-$name:local" --name "$CLUSTER" >/dev/null
done

kubectl apply -f deploy/k8s/namespace.yaml >/dev/null
# Secrets are generated here, never committed. Re-running keeps the existing ones (and the data they protect).
if ! kubectl -n "$NS" get secret ledgerline-secrets >/dev/null 2>&1; then
  kubectl -n "$NS" create secret generic ledgerline-secrets \
    --from-literal=postgres-password="$(openssl rand -hex 16)" \
    --from-literal=rabbitmq-password="$(openssl rand -hex 16)" \
    --from-literal=keycloak-admin-password="$(openssl rand -hex 16)" >/dev/null
fi
# The smoke test needs a token without a browser: a password-grant client is added for this cluster only,
# never to the realm deployed publicly.
REALM=$(mktemp -d)/ledgerline-realm.json
python3 - "$REALM" <<'PY'
import json, sys
realm = json.load(open('deploy/keycloak/ledgerline-realm.json'))
web = next(c for c in realm['clients'] if c['clientId'] == 'ledgerline-web')
realm['clients'].append({**web, 'clientId': 'ledgerline-smoke', 'directAccessGrantsEnabled': True,
                         'standardFlowEnabled': False, 'redirectUris': [], 'webOrigins': []})
web_id = web.get('id')
if web_id: realm['clients'][-1].pop('id')
json.dump(realm, open(sys.argv[1], 'w'))
PY
kubectl -n "$NS" create configmap keycloak-realm --from-file=ledgerline-realm.json="$REALM" \
  --dry-run=client -o yaml | kubectl apply -f - >/dev/null

kubectl apply -k deploy/k8s
# Images keep the tag "local": restart the apps so that a re-run picks up the freshly loaded ones.
kubectl -n "$NS" rollout restart deployment/ledger deployment/payments deployment/fraud deployment/gateway >/dev/null
for deployment in rabbitmq keycloak ledger payments fraud gateway; do
  kubectl -n "$NS" rollout status "deployment/$deployment" --timeout=300s
done
kubectl -n "$NS" rollout status statefulset/postgres --timeout=300s

echo "→ smoke test"
kubectl -n "$NS" port-forward svc/gateway 18080:8080 >/dev/null 2>&1 &
GATEWAY_PF=$!
kubectl -n "$NS" port-forward svc/keycloak 18081:8080 >/dev/null 2>&1 &
KEYCLOAK_PF=$!
trap 'kill $GATEWAY_PF $KEYCLOAK_PF 2>/dev/null || true' EXIT
sleep 3

test "$(curl -s -o /dev/null -w '%{http_code}' http://localhost:18080/api/ledger/accounts)" = 401
# The issuer must read http://keycloak:8080 (what the services expect), so ask Keycloak with that Host header.
TOKEN=$(curl -sf -H 'Host: keycloak:8080' http://localhost:18081/realms/ledgerline/protocol/openid-connect/token \
  -d grant_type=password -d client_id=ledgerline-smoke -d username=olivia -d password=ledgerline-demo \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["access_token"])')
curl -sf -H "Authorization: Bearer $TOKEN" http://localhost:18080/api/ledger/trial-balance \
  | python3 -c 'import json,sys; r=json.load(sys.stdin); assert r["balanced"] and r["journalEntries"] > 0, r; print("✓ books balanced,", r["journalEntries"], "entries, via gateway on Kubernetes")'
