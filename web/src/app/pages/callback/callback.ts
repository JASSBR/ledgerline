import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Auth } from '../../core/auth/auth';
import { homeFor } from '../../core/auth/guards';

/** Keycloak redirects here with ?code=…; the code is exchanged (with the PKCE verifier) and the user moves on. */
@Component({
  selector: 'app-callback',
  imports: [RouterLink],
  template: `
    <div class="center">
      @if (failed()) {
        <p class="alert" role="alert" i18n="@@callback.failed">
          La connexion n'a pas abouti (lien expiré ou déjà utilisé).
        </p>
        <a routerLink="/" class="button" i18n="@@callback.retry">Recommencer</a>
      } @else {
        <span class="spinner" aria-hidden="true"></span>
        <p class="muted" i18n="@@callback.progress">Connexion sécurisée en cours…</p>
      }
    </div>
  `,
  styles: `
    .center {
      min-height: 100vh;
      display: grid;
      place-content: center;
      justify-items: center;
      gap: 1rem;
    }
    .spinner {
      width: 1.6rem;
      height: 1.6rem;
      border-radius: 50%;
      border: 2px solid var(--border);
      border-top-color: var(--accent);
      animation: spin 0.7s linear infinite;
    }
    @keyframes spin {
      to {
        transform: rotate(360deg);
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Callback implements OnInit {
  private readonly auth = inject(Auth);
  private readonly router = inject(Router);
  protected readonly failed = signal(false);

  async ngOnInit(): Promise<void> {
    try {
      const returnUrl = await this.auth.completeLogin();
      const target = returnUrl === '/' ? homeFor(this.auth.isOperator()) : returnUrl;
      await this.router.navigateByUrl(target, { replaceUrl: true });
    } catch {
      this.failed.set(true);
    }
  }
}
