#!/usr/bin/env bash
# Builds the Angular SPA (French and English) and deploys it to Vercel as static files.
# Usage: API_URL=<gateway origin> AUTHORITY=<Keycloak realm URL> ./deploy/vercel.sh   (requires `vercel login`)
# Both values are printed by deploy/azure.sh.
set -euo pipefail
cd "$(dirname "$0")/.."
: "${API_URL:?Set API_URL to the gateway origin, e.g. https://ledgerline-bank.<env>.azurecontainerapps.io}"
: "${AUTHORITY:?Set AUTHORITY to the Keycloak realm URL, e.g. https://ledgerline-auth.<env>.azurecontainerapps.io/realms/ledgerline}"
API_HOST="${API_URL#https://}"
AUTH_ORIGIN=$(echo "$AUTHORITY" | sed -E 's#^(https://[^/]+).*#\1#')

# Both origins are compiled into the production build (and allowed by the CSP below).
sed -i.bak -E "s#apiBaseUrl: '[^']*'#apiBaseUrl: '${API_URL}'#; s#authority: '[^']*'#authority: '${AUTHORITY}'#" web/src/environments/environment.production.ts
rm -f web/src/environments/environment.production.ts.bak

(cd web && npm ci --no-audit --no-fund >/dev/null && npx ng build --configuration production >/dev/null)
OUT=web/dist/web/browser

# Strict CSP: scripts and fonts from the app itself only; calls only to the gateway (HTTP + SignalR) and Keycloak.
CSP="default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self'; connect-src 'self' https://${API_HOST} wss://${API_HOST} ${AUTH_ORIGIN}; frame-src 'none'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'"
cat > "$OUT/vercel.json" <<JSON
{
  "redirects": [
    { "source": "/", "has": [{ "type": "header", "key": "accept-language", "value": "en.*" }], "destination": "/en/", "permanent": false },
    { "source": "/", "destination": "/fr/", "permanent": false }
  ],
  "rewrites": [
    { "source": "/fr/:path*", "destination": "/fr/index.html" },
    { "source": "/en/:path*", "destination": "/en/index.html" }
  ],
  "headers": [
    {
      "source": "/(.*)",
      "headers": [
        { "key": "Content-Security-Policy", "value": "${CSP}" },
        { "key": "X-Content-Type-Options", "value": "nosniff" },
        { "key": "Referrer-Policy", "value": "strict-origin-when-cross-origin" },
        { "key": "Permissions-Policy", "value": "camera=(), microphone=(), geolocation=()" }
      ]
    },
    {
      "source": "/(fr|en)/(.*)-([A-Z0-9]{8}).(js|css|woff2)",
      "headers": [{ "key": "Cache-Control", "value": "public, max-age=31536000, immutable" }]
    }
  ]
}
JSON

cd "$OUT"
VERCEL=$(command -v vercel || echo "npx --yes vercel")
# The build wipes dist/, and with it the .vercel link: re-link to the existing project, never create a new one.
$VERCEL link --yes --project ledgerline >/dev/null 2>&1
DEPLOYMENT=$($VERCEL deploy --prod --yes 2>&1 | grep -Eo 'https://ledgerline-[a-z0-9]+-[a-z0-9-]+\.vercel\.app' | head -1)
[ -n "$DEPLOYMENT" ] || { echo "✗ Vercel deployment failed"; exit 1; }
# Stable public name for the CV and the README (the project's default alias is auto-generated).
$VERCEL alias set "$DEPLOYMENT" "${ALIAS:-ledgerline-bank.vercel.app}" >/dev/null
echo "✓ SPA: https://${ALIAS:-ledgerline-bank.vercel.app}  (deployment $DEPLOYMENT)"
