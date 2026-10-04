import { CurrencyPipe, DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { BankApi } from '../../banking/api';
import {
  Account,
  AccountEvent,
  AccountEventType,
  BalanceAsOf,
  StatementLine,
} from '../../banking/models';
import { problemMessages } from '../../shared/problem-details';
import { Auth } from '../../core/auth/auth';
import { TransfersRealtime } from '../../core/realtime/transfers-realtime';
import { reloadWhen } from '../../core/realtime/reload-when';
import { BalanceChart, BalancePoint } from '../../shared/balance-chart';
import { Icon } from '../../shared/icon';
import { Money } from '../../shared/money';

const today = (): string => new Date().toISOString().slice(0, 10);

const EVENT_LABELS: Readonly<Record<AccountEventType, string>> = {
  opened: $localize`:@@history.opened:Compte ouvert`,
  held: $localize`:@@history.held:Fonds réservés`,
  released: $localize`:@@history.released:Réservation libérée`,
  posted: $localize`:@@history.posted:Écriture passée`,
  frozen: $localize`:@@history.frozen:Compte gelé`,
  unfrozen: $localize`:@@history.unfrozen:Compte dégelé`,
};

@Component({
  selector: 'app-account-detail',
  imports: [CurrencyPipe, DatePipe, RouterLink, Icon, Money, BalanceChart],
  templateUrl: './account-detail.html',
  styleUrl: './account-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountDetail {
  protected readonly auth = inject(Auth);
  private readonly api = inject(BankApi);
  protected readonly eventLabels = EVENT_LABELS;
  readonly id = input.required<string>();

  protected readonly account = httpResource<Account>(() => `/api/ledger/accounts/${this.id()}`);
  protected readonly statement = httpResource<StatementLine[]>(
    () => ({ url: `/api/ledger/accounts/${this.id()}/statement`, params: { limit: 200 } }),
    { defaultValue: [] },
  );

  protected readonly tab = signal<'statement' | 'history'>('statement');
  // The stream is only read when asked for: most visits are about the balance, not the audit trail.
  protected readonly history = httpResource<AccountEvent[]>(
    () =>
      this.tab() === 'history'
        ? { url: `/api/ledger/accounts/${this.id()}/history`, params: { limit: 200 } }
        : undefined,
    { defaultValue: [] },
  );

  protected readonly freezeReason = signal('');
  protected readonly freezing = signal(false);
  protected readonly freezeError = signal<string | null>(null);

  protected readonly asOfDate = signal('');
  protected readonly maxDate = today();
  protected readonly balanceAsOf = httpResource<BalanceAsOf>(() => {
    const date = this.asOfDate();
    // End of the chosen day: entries with that value date are included.
    return date
      ? { url: `/api/ledger/accounts/${this.id()}/balance`, params: { asOf: `${date}T23:59:59Z` } }
      : undefined;
  });

  protected readonly chart = computed<BalancePoint[]>(() =>
    [...this.statement.value()]
      .reverse()
      .map((line) => ({ at: line.valueDate, balance: line.balanceAfter })),
  );

  protected readonly copied = signal(false);

  constructor() {
    const realtime = inject(TransfersRealtime);
    reloadWhen(
      () => [realtime.lastChange(), realtime.lastReceived()],
      this.account,
      this.statement,
      this.history,
    );
  }

  protected async copyIban(iban: string): Promise<void> {
    await navigator.clipboard?.writeText(iban);
    this.copied.set(true);
    setTimeout(() => this.copied.set(false), 1500);
  }

  protected toggleFreeze(account: Account): void {
    const reason = this.freezeReason().trim();
    if (!account.frozen && !reason) {
      this.freezeError.set($localize`:@@freeze.reasonRequired:Indiquez le motif du gel.`);
      return;
    }

    this.freezing.set(true);
    this.freezeError.set(null);
    const command = account.frozen
      ? this.api.unfreeze(account.id)
      : this.api.freeze(account.id, reason);
    command.subscribe({
      next: (updated) => {
        this.account.set(updated);
        this.freezeReason.set('');
        this.freezing.set(false);
        this.history.reload();
      },
      error: (error: HttpErrorResponse) => {
        this.freezeError.set(problemMessages(error).join(' '));
        this.freezing.set(false);
      },
    });
  }
}
