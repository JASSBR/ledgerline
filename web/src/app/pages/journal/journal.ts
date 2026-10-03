import { CurrencyPipe, DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { JournalEntry } from '../../banking/models';
import { TransfersRealtime } from '../../core/realtime/transfers-realtime';
import { reloadWhen } from '../../core/realtime/reload-when';
import { Icon } from '../../shared/icon';

@Component({
  selector: 'app-journal',
  imports: [CurrencyPipe, DatePipe, RouterLink, Icon],
  templateUrl: './journal.html',
  styleUrl: './journal.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Journal {
  protected readonly entries = httpResource<JournalEntry[]>(
    () => ({ url: '/api/ledger/journal', params: { limit: 100 } }),
    { defaultValue: [] },
  );

  constructor() {
    reloadWhen(inject(TransfersRealtime).lastChange, this.entries);
  }
}
