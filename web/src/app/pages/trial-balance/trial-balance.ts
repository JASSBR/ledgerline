import { CurrencyPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { BankApi } from '../../banking/api';
import { TrialBalance as TrialBalanceModel } from '../../banking/models';
import { TransfersRealtime } from '../../core/realtime/transfers-realtime';
import { reloadWhen } from '../../core/realtime/reload-when';
import { ToastService } from '../../core/toast';
import { Icon } from '../../shared/icon';
import { problemMessages } from '../../shared/problem-details';

@Component({
  selector: 'app-trial-balance',
  imports: [CurrencyPipe, RouterLink, Icon],
  templateUrl: './trial-balance.html',
  styleUrl: './trial-balance.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TrialBalance {
  private readonly api = inject(BankApi);
  private readonly toasts = inject(ToastService);

  protected readonly report = httpResource<TrialBalanceModel>(() => '/api/ledger/trial-balance');
  protected readonly customers = computed(() =>
    (this.report.value()?.accounts ?? []).filter((account) => account.kind === 'Customer'),
  );

  protected readonly depositAccount = signal('');
  protected readonly depositAmount = signal(500);
  protected readonly depositing = signal(false);
  protected readonly depositErrors = signal<string[]>([]);

  constructor() {
    reloadWhen(inject(TransfersRealtime).lastChange, this.report);
  }

  protected async deposit(event: Event): Promise<void> {
    event.preventDefault();
    const accountId = this.depositAccount() || this.customers()[0]?.accountId;
    if (!accountId) return;
    this.depositing.set(true);
    this.depositErrors.set([]);
    try {
      await firstValueFrom(
        this.api.deposit(
          accountId,
          this.depositAmount(),
          $localize`:@@deposit.reference:Dépôt guichet`,
        ),
      );
      this.toasts.show({ tone: 'success', title: $localize`:@@deposit.done:Dépôt comptabilisé` });
      this.report.reload();
    } catch (error) {
      this.depositErrors.set(problemMessages(error));
    } finally {
      this.depositing.set(false);
    }
  }
}
