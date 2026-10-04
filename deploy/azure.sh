#!/usr/bin/env bash
# Deploys Ledgerline to Azure Container Apps with Terraform (deploy/terraform).
# Images are built by GitHub Actions (.github/workflows/images.yml) and published on ghcr.io, tagged with the
# commit: this script deploys the commit you are on, once CI has published its images. Nothing is built here.
# Re-runnable: Terraform converges. Prerequisites: `az login`, Terraform, and HEAD pushed to GitHub.
# Settings come from deploy/terraform/terraform.tfvars (gitignored), see terraform.tfvars.example.
set -euo pipefail
cd "$(dirname "$0")/.."

TFVARS=deploy/terraform/terraform.tfvars
[ -f "$TFVARS" ] || { echo "✗ Missing $TFVARS (copy terraform.tfvars.example)"; exit 1; }
az account show >/dev/null 2>&1 || { echo "✗ Not logged in: run 'az login' first."; exit 1; }
TAG="sha-$(git rev-parse --short=7 HEAD)"
source deploy/ghcr.sh

for image in ledger payments fraud gateway keycloak; do
  wait_for_image "ledgerline-$image" "$TAG"
done

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
