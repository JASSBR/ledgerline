/**
 * End-to-end tests against the Kubernetes deployment (CI): Keycloak is port-forwarded and reached as
 * http://keycloak:8080 — the issuer the services validate — through a hosts entry pointing at 127.0.0.1.
 */
export const environment = {
  apiBaseUrl: '',
  oidc: { authority: 'http://keycloak:8080/realms/ledgerline', clientId: 'ledgerline-web' },
  repositoryUrl: 'https://github.com/JASSBR/ledgerline',
};
