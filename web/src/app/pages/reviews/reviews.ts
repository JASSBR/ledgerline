import { CurrencyPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { BankApi } from '../../banking/api';
import { formatIban } from '../../banking/iban';
import { RULE_LABELS, SCREENING_STATUS_LABELS } from '../../banking/labels';
import { Screening } from '../../banking/models';
import { TransfersRealtime } from '../../core/realtime/transfers-realtime';
import { reloadWhen } from '../../core/realtime/reload-when';
import { ToastService } from '../../core/toast';
import { Icon } from '../../shared/icon';
import { problemMessages } from '../../shared/problem-details';
import { RelativeTimePipe } from '../../shared/relative-time.pipe';

@Component({
  selector: 'app-reviews',
  imports: [CurrencyPipe, RouterLink, Icon, RelativeTimePipe],
  templateUrl: './reviews.html',
  styleUrl: './reviews.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Reviews {
  private readonly api = inject(BankApi);
  private readonly toasts = inject(ToastService);

  protected readonly queue = httpResource<Screening[]>(
    () => ({ url: '/api/fraud/screenings', params: { status: 'PendingReview' } }),
    { defaultValue: [] },
  );
  protected readonly history = httpResource<Screening[]>(() => '/api/fraud/screenings', {
    defaultValue: [],
  });

  protected readonly comments = signal<Readonly<Record<string, string>>>({});
  protected readonly deciding = signal<string | null>(null);
  protected readonly errors = signal<Readonly<Record<string, string[]>>>({});

  protected readonly ruleLabels = RULE_LABELS;
  protected readonly statusLabels = SCREENING_STATUS_LABELS;
  protected readonly formatIban = formatIban;

  constructor() {
    reloadWhen(inject(TransfersRealtime).lastChange, this.queue, this.history);
  }

  protected setComment(transferId: string, comment: string): void {
    this.comments.update((comments) => ({ ...comments, [transferId]: comment }));
  }

  protected async decide(screening: Screening, approve: boolean): Promise<void> {
    const comment = this.comments()[screening.transferId]?.trim() || null;
    this.deciding.set(screening.transferId);
    this.errors.update((errors) => ({ ...errors, [screening.transferId]: [] }));
    try {
      await firstValueFrom(this.api.decide(screening.transferId, approve, comment));
      this.toasts.show({
        tone: approve ? 'success' : 'info',
        title: approve
          ? $localize`:@@reviews.approved:Virement approuvé : exécution en cours`
          : $localize`:@@reviews.rejected:Virement rejeté : fonds rendus au client`,
      });
      this.queue.reload();
      this.history.reload();
    } catch (error) {
      this.errors.update((errors) => ({
        ...errors,
        [screening.transferId]: problemMessages(error),
      }));
    } finally {
      this.deciding.set(null);
    }
  }
}
