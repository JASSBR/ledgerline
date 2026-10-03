#!/usr/bin/env bash
# Wakes the three portfolio demos (free tiers sleep when idle) and waits until each one really answers.
# Run it a few minutes before sending an application or starting an interview:
#   GitHub → Actions → "Wake the demos" → Run workflow     (or locally: ./eng/wake-demos.sh)
set -uo pipefail

CLAIMFLOW_API=https://claimflow-api.lemondune-f6dce829.italynorth.azurecontainerapps.io
LEDGERLINE_AUTH=https://ledgerline-auth.lemondune-f6dce829.italynorth.azurecontainerapps.io
LEDGERLINE_BANK=https://ledgerline-bank.lemondune-f6dce829.italynorth.azurecontainerapps.io
COMPTOIR=https://comptoir-facade.lemondune-f6dce829.italynorth.azurecontainerapps.io
DEADLINE=$((SECONDS + 300))

# until <label> <expected status> <curl arguments…>: retries until the URL answers the expected status.
until_ok() {
  local label=$1 expected=$2; shift 2
  local started=$SECONDS status
  while [ $SECONDS -lt $DEADLINE ]; do
    status=$(curl -s -o /dev/null -m 60 -w '%{http_code}' "$@")
    if [ "$status" = "$expected" ]; then
      echo "✓ $label ($((SECONDS - started)) s)"
      return 0
    fi
    sleep 5
  done
  echo "✗ $label: still answering $status after 5 minutes"
  return 1
}

claimflow() {
  until_ok "ClaimFlow API" 200 "$CLAIMFLOW_API/health"
}

ledgerline() {
  until_ok "Ledgerline Keycloak" 200 "$LEDGERLINE_AUTH/realms/ledgerline/.well-known/openid-configuration" &&
    until_ok "Ledgerline gateway + services" 200 "$LEDGERLINE_BANK/health" &&
    # 401: the Ledger service itself answered (it validates tokens against Keycloak).
    until_ok "Ledgerline ledger service" 401 "$LEDGERLINE_BANK/api/ledger/accounts"
}

comptoir() {
  local jar token
  jar=$(mktemp)
  until_ok "Comptoir facade + 2014 application (IIS)" 200 "$COMPTOIR/Account/Login" || return 1
  # Signing in wakes the database; reading the catalogue wakes the new API. Demo account, published password.
  token=$(curl -s -m 60 -c "$jar" -b "$jar" "$COMPTOIR/Account/Login" |
    sed -nE 's/.*name="__RequestVerificationToken" type="hidden" value="([^"]+)".*/\1/p' | head -1)
  until_ok "Comptoir database (sign-in)" 302 -c "$jar" -b "$jar" --data-urlencode "__RequestVerificationToken=$token" \
    -d "login=sophie&password=comptoir-demo" "$COMPTOIR/Account/Login" &&
    until_ok "Comptoir new API" 200 -b "$jar" "$COMPTOIR/api/products"
  local result=$?
  rm -f "$jar"
  return $result
}

claimflow > /tmp/wake-claimflow.log 2>&1 & a=$!
ledgerline > /tmp/wake-ledgerline.log 2>&1 & b=$!
comptoir > /tmp/wake-comptoir.log 2>&1 & c=$!
failed=0
for pid in $a $b $c; do wait "$pid" || failed=1; done
cat /tmp/wake-claimflow.log /tmp/wake-ledgerline.log /tmp/wake-comptoir.log
exit $failed
