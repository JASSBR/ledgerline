import { CurrencyPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Account } from '../../banking/models';
import { TransfersStore } from '../../banking/transfers.store';
import { Auth } from '../../core/auth/auth';
import { TransfersRealtime } from '../../core/realtime/transfers-realtime';
import { reloadWhen } from '../../core/realtime/reload-when';
import { Icon } from '../../shared/icon';
import { RelativeTimePipe } from '../../shared/relative-time.pipe';
import { StatusBadge } from '../../shared/status-badge';

const RECENT_TRANSFERS = 5;

@Component({
  selector: 'app-accounts',
  imports: [CurrencyPipe, RouterLink, Icon, StatusBadge, RelativeTimePipe],
  templateUrl: './accounts.html',
  styleUrl: './accounts.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Accounts {
  protected readonly auth = inject(Auth);
  protected readonly store = inject(TransfersStore);

  protected readonly accounts = httpResource<Account[]>(() => '/api/ledger/accounts', {
    defaultValue: [],
  });
  protected readonly total = computed(() =>
    this.accounts.value().reduce((sum, account) => sum + account.balance, 0),
  );
  protected readonly held = computed(() =>
    this.accounts.value().reduce((sum, account) => sum + account.held, 0),
  );
  protected readonly firstName = computed(() => this.auth.user()?.name.split(' ')[0] ?? '');
  protected readonly recent = computed(() => this.store.transfers().slice(0, RECENT_TRANSFERS));

  constructor() {
    // Every transfer step can move a hold or a balance: refresh the figures, keep the cards on screen.
    const realtime = inject(TransfersRealtime);
    reloadWhen(() => [realtime.lastChange(), realtime.lastReceived()], this.accounts);
  }
}
