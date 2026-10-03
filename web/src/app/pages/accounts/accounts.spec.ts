import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TransfersStore } from '../../banking/transfers.store';
import { account, fakeAuth, fakeRealtime, transfer } from '../../testing';
import { Accounts } from './accounts';

describe('Accounts', () => {
  let http: HttpTestingController;
  let realtime: ReturnType<typeof fakeRealtime>['fake'];

  beforeEach(() => {
    const live = fakeRealtime();
    realtime = live.fake;
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        fakeAuth().provider,
        live.provider,
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('greets the customer, totals the accounts and shows money on hold', async () => {
    TestBed.inject(TransfersStore).upsert(transfer({ toName: 'Bob Durand' }));
    const fixture = TestBed.createComponent(Accounts);
    fixture.detectChanges();
    http
      .expectOne('/api/ledger/accounts')
      .flush([
        account({ balance: 6400, available: 2400, held: 4000 }),
        account({ id: 'acc-2', name: 'Livret', balance: 12000, available: 12000 }),
      ]);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('h1')!.textContent).toContain('Bonjour Alice');
    expect(element.querySelector('.total')!.textContent).toMatch(/18\s?400,00/);
    expect(element.querySelector('.held')!.textContent).toMatch(/4\s?000,00/);
    expect(element.querySelectorAll('.account')).toHaveLength(2);
    expect(element.querySelector('.transfers')!.textContent).toContain('Bob Durand');
  });

  it('refreshes balances when a transfer moves, keeping the cards on screen', async () => {
    const fixture = TestBed.createComponent(Accounts);
    fixture.detectChanges();
    http.expectOne('/api/ledger/accounts').flush([account()]);
    await fixture.whenStable();

    realtime.lastChange.set({
      transferId: 't1',
      ownerId: 'x',
      status: 'Completed',
      reason: null,
      occurredAt: '',
    });
    TestBed.tick();
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('.account')).toHaveLength(1);
    http.expectOne('/api/ledger/accounts').flush([account({ balance: 6000 })]);
    // The transfers store follows the same notification.
    http.expectOne('/api/payments/transfers/t1').flush(transfer());
  });
});
