import { environment } from '../../environments/environment';
import { warmUpBackends } from './warm-up';

describe('warmUpBackends', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('pings Keycloak and the gateway without waiting for them, and ignores failures', async () => {
    const fetch = vi.fn(async () => {
      throw new Error('asleep');
    });
    vi.stubGlobal('fetch', fetch);

    warmUpBackends();
    await Promise.resolve();

    expect(fetch).toHaveBeenCalledWith(
      `${environment.oidc.authority}/.well-known/openid-configuration`,
      { mode: 'no-cors', cache: 'no-store' },
    );
    expect(fetch).toHaveBeenCalledWith(`${environment.apiBaseUrl}/health`, {
      mode: 'no-cors',
      cache: 'no-store',
    });
  });
});
