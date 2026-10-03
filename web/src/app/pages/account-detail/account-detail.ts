import { CurrencyPipe, DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Account, BalanceAsOf, StatementLine } from '../../banking/models';
import { Auth } from '../../core/auth/auth';
import { TransfersRealtime } from '../../core/realtime/transfers-realtime';
import { reloadWhen } from '../../core/realtime/reload-when';
import { BalanceChart, BalancePoint } from '../../shared/balance-chart';
import { Icon } from '../../shared/icon';

const today = (): string => new Date().toISOString().slice(0, 10);

@Component({
  selector: 'app-account-detail',
  imports: [CurrencyPipe, DatePipe, RouterLink, Icon, BalanceChart],
  templateUrl: './account-detail.html',
  styleUrl: './account-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountDetail {
  protected readonly auth = inject(Auth);
  readonly id = input.required<string>();

  protected readonly account = httpResource<Account>(() => `/api/ledger/accounts/${this.id()}`);
  protected readonly statement = httpResource<StatementLine[]>(
    () => ({ url: `/api/ledger/accounts/${this.id()}/statement`, params: { limit: 200 } }),
    { defaultValue: [] },
  );

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
    reloadWhen(inject(TransfersRealtime).lastChange, this.account, this.statement);
  }

  protected async copyIban(iban: string): Promise<void> {
    await navigator.clipboard?.writeText(iban);
    this.copied.set(true);
    setTimeout(() => this.copied.set(false), 1500);
  }
}
