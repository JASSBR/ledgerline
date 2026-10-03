#!/usr/bin/env bash
# Deploys Ledgerline to Azure Container Apps with Terraform (deploy/terraform), images built locally.
# Re-runnable: Terraform converges; each run ships a fresh image tag.
# Prerequisites: `az login`, Docker, Terraform. Settings come from deploy/terraform/terraform.tfvars (gitignored),
# see terraform.tfvars.example.
#
# Azure for Students: ACR Tasks (remote builds) is forbidden, so images are built here — for linux/amd64,
# mandatory on Apple Silicon.
set -euo pipefail
cd "$(dirname "$0")/.."

TFVARS=deploy/terraform/terraform.tfvars
[ -f "$TFVARS" ] || { echo "✗ Missing $TFVARS (copy terraform.tfvars.example)"; exit 1; }
az account show >/dev/null 2>&1 || { echo "✗ Not logged in: run 'az login' first."; exit 1; }
ACR=$(sed -nE 's/^registry_name *= *"([^"]+)".*/\1/p' "$TFVARS")
REGISTRY=$(az acr show -n "$ACR" --query loginServer -o tsv)
TAG="$(git rev-parse --short HEAD)-$(date +%H%M%S)"

az acr login -n "$ACR" --only-show-errors >/dev/null
build() { # name, build args...
  echo "→ image ledgerline-$1:$TAG"
  local name=$1; shift
  docker buildx build --platform linux/amd64 --push -q -t "$REGISTRY/ledgerline-$name:$TAG" "$@" >/dev/null
}
build ledger --build-arg PROJECT=src/Services/Ledger/Ledgerline.Ledger/Ledgerline.Ledger.csproj .
build payments --build-arg PROJECT=src/Services/Payments/Ledgerline.Payments/Ledgerline.Payments.csproj .
build fraud --build-arg PROJECT=src/Services/Fraud/Ledgerline.Fraud/Ledgerline.Fraud.csproj .
build gateway --build-arg PROJECT=src/Ledgerline.Gateway/Ledgerline.Gateway.csproj .
build keycloak deploy/keycloak

echo "→ terraform apply"
cd deploy/terraform
terraform init -input=false >/dev/null
terraform apply -input=false -auto-approve -var "image_tag=$TAG" >/dev/null
GATEWAY=$(terraform output -raw gateway_url)
AUTHORITY=$(terraform output -raw authority)

echo "→ waiting for $GATEWAY (scale from zero, then migrations and seed)"
for _ in $(seq 1 60); do
  [ "$(curl -s -o /dev/null -w '%{http_code}' "$GATEWAY/health")" = 200 ] && break
  sleep 5
done
echo "✓ gateway:   $GATEWAY"
echo "✓ authority: $AUTHORITY"
echo "  next: API_URL=$GATEWAY AUTHORITY=$AUTHORITY ./deploy/vercel.sh"
