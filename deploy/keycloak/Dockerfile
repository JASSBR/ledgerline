# Keycloak with the demo realm baked in, for platforms without config volumes (Azure Container Apps).
#   docker build -t ledgerline-keycloak deploy/keycloak
FROM quay.io/keycloak/keycloak:26.6
COPY ledgerline-realm.json /opt/keycloak/data/import/ledgerline-realm.json
