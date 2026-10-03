/** Development: Keycloak runs in Aspire on localhost:8080; API calls go through the dev-server proxy. */
export const environment = {
  apiBaseUrl: '',
  oidc: { authority: 'https://localhost:8080/realms/ledgerline', clientId: 'ledgerline-web' },
  repositoryUrl: 'https://github.com/JASSBR/ledgerline',
};
