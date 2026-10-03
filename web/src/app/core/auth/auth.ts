import { DOCUMENT } from '@angular/common';
import { Injectable, InjectionToken, computed, inject, signal } from '@angular/core';
import { User, UserManager, WebStorageStateStore } from 'oidc-client-ts';
import { environment } from '../../../environments/environment';

export interface CurrentUser {
  readonly id: string;
  readonly username: string;
  readonly name: string;
  readonly roles: readonly string[];
}

/** Keycloak realm roles, emitted as a flat "roles" claim by the realm's protocol mapper. */
export const ROLES = { customer: 'customer', operator: 'operator' } as const;

/** The oidc-client-ts manager, behind a token so tests can stand in for Keycloak. */
export const USER_MANAGER = new InjectionToken<UserManager>('USER_MANAGER', {
  providedIn: 'root',
  factory: () => {
    const document = inject(DOCUMENT);
    return new UserManager({
      authority: environment.oidc.authority,
      client_id: environment.oidc.clientId,
      redirect_uri: new URL('callback', document.baseURI).href,
      post_logout_redirect_uri: document.baseURI,
      response_type: 'code',
      scope: 'openid profile',
      automaticSilentRenew: true,
      userStore: new WebStorageStateStore({ store: globalThis.sessionStorage }),
    });
  },
});

/**
 * OpenID Connect, authorization code flow with PKCE, against Keycloak — the same protocol a real bank front-end uses.
 * Tokens live in sessionStorage (closing the tab ends the session) and are renewed with the refresh token.
 */
@Injectable({ providedIn: 'root' })
export class Auth {
  private readonly manager = inject(USER_MANAGER);
  private readonly session = signal<User | null>(null);

  readonly user = computed<CurrentUser | null>(() => {
    const session = this.session();
    if (!session || session.expired) return null;
    const profile = session.profile as Record<string, unknown>;
    return {
      id: session.profile.sub,
      username: String(profile['preferred_username'] ?? ''),
      name: String(profile['name'] ?? profile['preferred_username'] ?? ''),
      roles: Array.isArray(profile['roles']) ? (profile['roles'] as string[]) : [],
    };
  });
  readonly isAuthenticated = computed(() => this.user() !== null);
  readonly isOperator = computed(() => this.user()?.roles.includes(ROLES.operator) ?? false);

  constructor() {
    this.manager.events.addUserLoaded((user) => this.session.set(user));
    this.manager.events.addUserUnloaded(() => this.session.set(null));
  }

  /** Restores a session from storage at startup (run by the app initializer, before the first navigation). */
  async restore(): Promise<void> {
    this.session.set(await this.manager.getUser());
  }

  accessToken(): string | null {
    return this.session()?.access_token ?? null;
  }

  /** Redirects to Keycloak; login_hint pre-fills the demo username so only the password is left to type. */
  login(username?: string, returnUrl = '/'): Promise<void> {
    return this.manager.signinRedirect({ login_hint: username, state: returnUrl });
  }

  /** Completes the redirect: exchanges the code (with the PKCE verifier) for tokens. Returns where to go next. */
  async completeLogin(): Promise<string> {
    const user = await this.manager.signinRedirectCallback();
    this.session.set(user);
    return typeof user.state === 'string' ? user.state : '/';
  }

  logout(): Promise<void> {
    return this.manager.signoutRedirect();
  }
}
