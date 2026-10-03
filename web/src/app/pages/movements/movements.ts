import { CurrencyPipe, DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  LOCALE_ID,
  computed,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { Account, Movements as MovementsReport } from '../../banking/models';
import { movementsCsv } from '../../banking/movements-csv';
import { Auth } from '../../core/auth/auth';
import { TransfersRealtime } from '../../core/realtime/transfers-realtime';
import { reloadWhen } from '../../core/realtime/reload-when';
import { FlowChart } from '../../shared/flow-chart';
import { Icon } from '../../shared/icon';

export type Period = '30' | '90' | '365' | 'all';
export type Direction = 'all' | 'in' | 'out';

const DAY_MS = 86_400_000;
const EMPTY: MovementsReport = { moneyIn: 0, moneyOut: 0, net: 0, months: [], lines: [] };

@Component({
  selector: 'app-movements',
  imports: [CurrencyPipe, DatePipe, RouterLink, Icon, FlowChart],
  templateUrl: './movements.html',
  styleUrl: './movements.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Movements {
  protected readonly auth = inject(Auth);
  private readonly locale = inject(LOCALE_ID);
  private readonly document = inject(DOCUMENT);

  protected readonly period = signal<Period>('90');
  protected readonly direction = signal<Direction>('all');
  protected readonly accountId = signal('');

  protected readonly accounts = httpResource<Account[]>(() => '/api/ledger/accounts', {
    defaultValue: [],
  });
  protected readonly customerAccounts = computed(() =>
    this.accounts.value().filter((account) => account.kind === 'Customer'),
  );

  protected readonly report = httpResource<MovementsReport>(
    () => {
      const params: Record<string, string> = {};
      const period = this.period();
      if (period !== 'all') {
        // Whole days: the period starts at midnight UTC, so the same filter gives the same lines all day long.
        const start = new Date(Date.now() - Number(period) * DAY_MS);
        params['from'] = `${start.toISOString().slice(0, 10)}T00:00:00Z`;
      }
      if (this.direction() !== 'all') params['direction'] = this.direction();
      if (this.accountId()) params['accountId'] = this.accountId();
      return { url: '/api/ledger/movements', params };
    },
    { defaultValue: EMPTY },
  );

  protected readonly showAccount = computed(
    () => !this.accountId() && this.customerAccounts().length > 1,
  );

  constructor() {
    const realtime = inject(TransfersRealtime);
    reloadWhen(() => [realtime.lastChange(), realtime.lastReceived()], this.report);
  }

  protected exportCsv(): void {
    const csv = movementsCsv(this.report.value().lines, this.locale);
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const link = this.document.createElement('a');
    link.href = url;
    link.download = `ledgerline-mouvements-${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }
}
