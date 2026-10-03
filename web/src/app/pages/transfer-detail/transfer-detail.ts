import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TRANSFER_STATUS_LABELS } from '../../banking/labels';
import { formatIban } from '../../banking/iban';
import { isTerminal } from '../../banking/models';
import { StageOwner, settlementTime, transferStages } from '../../banking/transfer-progress';
import { TransfersStore } from '../../banking/transfers.store';
import { Auth } from '../../core/auth/auth';
import { Icon } from '../../shared/icon';
import { StatusBadge } from '../../shared/status-badge';

@Component({
  selector: 'app-transfer-detail',
  imports: [CurrencyPipe, DatePipe, DecimalPipe, RouterLink, Icon, StatusBadge],
  templateUrl: './transfer-detail.html',
  styleUrl: './transfer-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TransferDetail {
  private readonly store = inject(TransfersStore);
  protected readonly auth = inject(Auth);
  readonly id = input.required<string>();

  protected readonly missing = signal(false);
  protected readonly transfer = computed(() =>
    this.store.transfers().find((transfer) => transfer.id === this.id()),
  );
  protected readonly stages = computed(() => {
    const transfer = this.transfer();
    return transfer ? transferStages(transfer) : [];
  });
  protected readonly elapsed = computed(() => {
    const transfer = this.transfer();
    return transfer ? settlementTime(transfer) : null;
  });
  protected readonly inFlight = computed(() => {
    const transfer = this.transfer();
    return transfer !== undefined && !isTerminal(transfer.status);
  });

  protected readonly statusLabels = TRANSFER_STATUS_LABELS;
  protected readonly formatIban = formatIban;
  protected readonly owners: Readonly<Record<StageOwner, string>> = {
    ledger: $localize`:@@owner.ledger:Service Ledger`,
    fraud: $localize`:@@owner.fraud:Service Fraud`,
    analyst: $localize`:@@owner.analyst:Analyste conformité`,
    payments: $localize`:@@owner.payments:Service Payments`,
  };

  constructor() {
    // Opened from a link: the store may not hold it yet. Afterwards SignalR keeps it current.
    effect(() => {
      const id = this.id();
      untracked(() => {
        this.missing.set(false);
        this.store.refresh(id).catch(() => this.missing.set(true));
      });
    });
  }
}
