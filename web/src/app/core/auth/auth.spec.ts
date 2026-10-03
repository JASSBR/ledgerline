import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { User } from 'oidc-client-ts';
import { fakeAuth, OLIVIA } from '../../testing';
import { Auth, USER_MANAGER } from './auth';
import { authInterceptor } from './auth.interceptor';
import { customerGuard, guestGuard, homeFor, operatorGuard, signedInGuard } from './guards';

function oidcUser(profile: Record<string, unknown>, expired = false): User {
  return {
    access_token: 'access-token',
    expired,
    state: '/transfers',
    profile: { sub: 'user-1', ...profile },
  } as unknown as User;
}

function fakeManager(stored: User | null = null) {
  const listeners: { loaded?: (user: User) => void; unloaded?: () => void } = {};
  return {
    events: {
      addUserLoaded: (callback: (user: User) => void) => (listeners.loaded = callback),
      addUserUnloaded: (callback: () => void) => (listeners.unloaded = callback),
    },
    getUser: vi.fn(async () => stored),
    signinRedirect: vi.fn(async () => undefined),
    signinRedirectCallback: vi.fn(async () =>
      oidcUser({ preferred_username: 'alice', name: 'Alice Martin', roles: ['customer'] }),
    ),
    signoutRedirect: vi.fn(async () => undefined),
    listeners,
  };
}

describe('Auth', () => {
  function setup(manager = fakeManager()) {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: USER_MANAGER, useValue: manager },
      ],
    });
    return { auth: TestBed.inject(Auth), manager, http: TestBed.inject(HttpTestingController) };
  }

  it('restores a stored session and reads the Keycloak claims', async () => {
    const { auth } = setup(
      fakeManager(
        oidcUser({ preferred_username: 'olivia', name: 'Olivia Roux', roles: ['operator'] }),
      ),
    );
    await auth.restore();

    expect(auth.user()).toEqual({
      id: 'user-1',
      username: 'olivia',
      name: 'Olivia Roux',
      roles: ['operator'],
    });
    expect(auth.isOperator()).toBe(true);
    expect(auth.accessToken()).toBe('access-token');
  });

  it('treats an expired session as signed out', async () => {
    const { auth } = setup(fakeManager(oidcUser({ preferred_username: 'alice' }, true)));
    await auth.restore();
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('pre-fills the username at Keycloak and carries the return URL in the state', async () => {
    const { auth, manager } = setup();
    await auth.login('bob', '/transfers');
    expect(manager.signinRedirect).toHaveBeenCalledWith({ login_hint: 'bob', state: '/transfers' });
  });

  it('completes the code exchange and returns where the user was going', async () => {
    const { auth } = setup();
    expect(await auth.completeLogin()).toBe('/transfers');
    expect(auth.user()?.username).toBe('alice');
  });

  it('follows silent renewals and sign-outs from other tabs', () => {
    const { auth, manager } = setup();
    manager.listeners.loaded!(oidcUser({ preferred_username: 'alice' }));
    expect(auth.isAuthenticated()).toBe(true);
    manager.listeners.unloaded!();
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('sends the token to the API only, and signs in again on 401', async () => {
    const { auth, manager, http } = setup(fakeManager(oidcUser({ preferred_username: 'alice' })));
    await auth.restore();
    const client = TestBed.inject(HttpClient);

    client.get('/api/ledger/accounts').subscribe({ error: () => undefined });
    client.get('https://keycloak.example/realms/x').subscribe();

    const api = http.expectOne('/api/ledger/accounts');
    expect(api.request.headers.get('Authorization')).toBe('Bearer access-token');
    const external = http.expectOne('https://keycloak.example/realms/x');
    expect(external.request.headers.has('Authorization')).toBe(false);

    api.flush(null, { status: 401, statusText: 'Unauthorized' });
    external.flush({});
    expect(manager.signinRedirect).toHaveBeenCalled();
    http.verify();
  });

  it('signs out through Keycloak', async () => {
    const { auth, manager } = setup();
    await auth.logout();
    expect(manager.signoutRedirect).toHaveBeenCalled();
  });
});

describe('guards', () => {
  function run(guard: typeof signedInGuard, user = fakeAuth()) {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [provideRouter([]), user.provider] });
    const result = TestBed.runInInjectionContext(() => guard({} as never, {} as never));
    return result instanceof UrlTree ? TestBed.inject(Router).serializeUrl(result) : result;
  }

  it('sends each role to its home', () => {
    expect(homeFor(false)).toBe('/accounts');
    expect(homeFor(true)).toBe('/ops/reviews');
  });

  it('keeps visitors on the welcome page and signed-in users away from it', () => {
    expect(run(guestGuard, fakeAuth(null))).toBe(true);
    expect(run(guestGuard)).toBe('/accounts');
  });

  it('requires a session', () => {
    expect(run(signedInGuard)).toBe(true);
    expect(run(signedInGuard, fakeAuth(null))).toBe('/');
  });

  it('separates customer and operator areas', () => {
    expect(run(operatorGuard)).toBe('/accounts');
    expect(run(operatorGuard, fakeAuth(OLIVIA))).toBe(true);
    expect(run(customerGuard, fakeAuth(OLIVIA))).toBe('/ops/reviews');
    expect(run(customerGuard)).toBe(true);
  });
});
