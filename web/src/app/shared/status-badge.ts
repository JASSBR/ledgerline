import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TRANSFER_STATUS_LABELS } from '../banking/labels';
import { TransferStatus } from '../banking/models';

@Component({
  selector: 'app-status-badge',
  template: `<span class="badge" [attr.data-tone]="tone()"
    ><span class="dot" [class.live]="tone() === 'progress'"></span>{{ label() }}</span
  >`,
  styles: `
    .badge {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      padding: 0.2rem 0.65rem 0.2rem 0.5rem;
      border-radius: 999px;
      font-size: 0.78rem;
      font-weight: 600;
      white-space: nowrap;
      color: var(--status);
      background: color-mix(in srgb, var(--status) 12%, transparent);
    }
    .dot {
      width: 0.45rem;
      height: 0.45rem;
      border-radius: 50%;
      background: currentColor;
    }
    .dot.live {
      animation: pulse 1.4s ease-in-out infinite;
    }
    @keyframes pulse {
      50% {
        opacity: 0.3;
      }
    }
    [data-tone='progress'] {
      --status: var(--status-declared);
    }
    [data-tone='review'] {
      --status: var(--status-review);
    }
    [data-tone='done'] {
      --status: var(--status-approved);
    }
    [data-tone='failed'] {
      --status: var(--status-rejected);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusBadge {
  readonly status = input.required<TransferStatus>();
  protected readonly label = computed(() => TRANSFER_STATUS_LABELS[this.status()]);
  protected readonly tone = computed(() => {
    switch (this.status()) {
      case 'Completed':
        return 'done';
      case 'Rejected':
      case 'Failed':
        return 'failed';
      case 'PendingReview':
        return 'review';
      default:
        return 'progress';
    }
  });
}
