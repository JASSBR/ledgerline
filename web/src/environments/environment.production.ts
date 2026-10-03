/**
 * Production: the SPA (Vercel) calls the gateway (Azure Container Apps) directly and signs in with the hosted
 * Keycloak. deploy/vercel.sh writes both URLs here before building.
 */
export const environment = {
  apiBaseUrl: 'https://ledgerline-bank.lemondune-f6dce829.italynorth.azurecontainerapps.io',
  oidc: {
    authority:
      'https://ledgerline-auth.lemondune-f6dce829.italynorth.azurecontainerapps.io/realms/ledgerline',
    clientId: 'ledgerline-web',
  },
  repositoryUrl: 'https://github.com/JASSBR/ledgerline',
};
