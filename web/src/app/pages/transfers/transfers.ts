import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Transfer, isTerminal } from '../../banking/models';
import { TransfersStore } from '../../banking/transfers.store';
import { Auth } from '../../core/auth/auth';
import { Icon } from '../../shared/icon';
import { RelativeTimePipe } from '../../shared/relative-time.pipe';
import { StatusBadge } from '../../shared/status-badge';

type Filter = 'all' | 'inFlight' | 'review' | 'completed' | 'unsuccessful';

const MATCHES: Readonly<Record<Filter, (transfer: Transfer) => boolean>> = {
  all: () => true,
  inFlight: (transfer) => !isTerminal(transfer.status),
  review: (transfer) => transfer.status === 'PendingReview',
  completed: (transfer) => transfer.status === 'Completed',
  unsuccessful: (transfer) => transfer.status === 'Rejected' || transfer.status === 'Failed',
};

@Component({
  selector: 'app-transfers',
  imports: [CurrencyPipe, RouterLink, Icon, StatusBadge, RelativeTimePipe],
  templateUrl: './transfers.html',
  styleUrl: './transfers.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Transfers {
  protected readonly auth = inject(Auth);
  protected readonly store = inject(TransfersStore);

  protected readonly filter = signal<Filter>('all');
  protected readonly filters: readonly { value: Filter; label: string }[] = [
    { value: 'all', label: $localize`:@@filter.all:Tous` },
    { value: 'inFlight', label: $localize`:@@filter.inFlight:En cours` },
    { value: 'review', label: $localize`:@@filter.review:En revue` },
    { value: 'completed', label: $localize`:@@filter.completed:Exécutés` },
    { value: 'unsuccessful', label: $localize`:@@filter.unsuccessful:Refusés ou échoués` },
  ];

  protected readonly visible = computed(() =>
    this.store.transfers().filter(MATCHES[this.filter()]),
  );
  protected readonly counts = computed(() => {
    const transfers = this.store.transfers();
    return Object.fromEntries(
      this.filters.map(({ value }) => [value, transfers.filter(MATCHES[value]).length]),
    ) as Record<Filter, number>;
  });
}
