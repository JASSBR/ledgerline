import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ToastService } from '../core/toast';
import { Icon } from './icon';

@Component({
  selector: 'app-toast-host',
  imports: [Icon],
  template: `
    <section
      class="host"
      aria-live="polite"
      i18n-aria-label="@@toast.region"
      aria-label="Notifications"
    >
      @for (toast of toasts.toasts(); track toast.id) {
        <div class="toast" [attr.data-tone]="toast.tone">
          <app-icon
            [name]="
              toast.tone === 'error' ? 'alert' : toast.tone === 'success' ? 'check' : 'activity'
            "
          />
          <div>
            <strong>{{ toast.title }}</strong>
            @if (toast.message) {
              <p>{{ toast.message }}</p>
            }
          </div>
          <button
            type="button"
            class="close"
            (click)="toasts.dismiss(toast.id)"
            i18n-aria-label="@@common.close"
            aria-label="Fermer"
          >
            <app-icon name="x" [size]="14" />
          </button>
        </div>
      }
    </section>
  `,
  styles: `
    .host {
      position: fixed;
      right: 1rem;
      bottom: 1rem;
      display: grid;
      gap: 0.6rem;
      z-index: 50;
      width: min(380px, calc(100vw - 2rem));
    }
    .toast {
      display: flex;
      align-items: flex-start;
      gap: 0.7rem;
      padding: 0.85rem 0.9rem;
      border-radius: var(--radius);
      background: var(--surface);
      border: 1px solid var(--border);
      box-shadow: var(--shadow-lg);
      animation: slide-in 0.25s ease-out;
      font-size: 0.88rem;
    }
    .toast app-icon {
      color: var(--accent);
      margin-top: 0.1rem;
    }
    [data-tone='success'] app-icon {
      color: var(--status-approved);
    }
    [data-tone='error'] app-icon {
      color: var(--status-rejected);
    }
    .toast div {
      flex: 1;
    }
    .toast p {
      margin: 0.2rem 0 0;
      color: var(--muted);
    }
    .close {
      border: 0;
      background: none;
      color: var(--muted);
      padding: 0.1rem;
      cursor: pointer;
    }
    @keyframes slide-in {
      from {
        transform: translateY(8px);
        opacity: 0;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ToastHost {
  protected readonly toasts = inject(ToastService);
}
