import { environment } from '../../environments/environment';

/**
 * The demo backends scale to zero when idle, and Keycloak takes the longest to come back. Pinging them as soon as
 * the welcome page opens lets them start while the visitor reads it, instead of after the click on a profile.
 * Fire and forget: the answers are opaque (no-cors) and failures don't matter.
 */
export function warmUpBackends(): void {
  const urls = [
    `${environment.oidc.authority}/.well-known/openid-configuration`,
    `${environment.apiBaseUrl}/health`,
  ];
  for (const url of urls) {
    globalThis.fetch?.(url, { mode: 'no-cors', cache: 'no-store' }).catch(() => undefined);
  }
}
