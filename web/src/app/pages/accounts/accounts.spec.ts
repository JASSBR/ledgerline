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
    const month = http.expectOne((r) => r.url === '/api/ledger/movements');
    expect(month.request.params.get('from')).toMatch(/-01T00:00:00\.000Z$/);
    month.flush({ moneyIn: 7300, moneyOut: 900, net: 6400, months: [], lines: [] });
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('h1')!.textContent).toContain('Bonjour Alice');
    expect(element.querySelector('.total')!.textContent).toMatch(/18\s?400,00/);
    expect(element.querySelector('.held')!.textContent).toMatch(/4\s?000,00/);
    expect(element.querySelectorAll('.account')).toHaveLength(2);
    expect(element.querySelector('.transfers')!.textContent).toContain('Bob Durand');
    expect(element.querySelector('.flow.in')!.textContent).toMatch(/\+7\s?300,00/);
    expect(element.querySelector('.flow.out')!.textContent).toMatch(/900,00/);
  });

  it('refreshes balances when a transfer moves, keeping the cards on screen', async () => {
    const fixture = TestBed.createComponent(Accounts);
    fixture.detectChanges();
    http.expectOne('/api/ledger/accounts').flush([account()]);
    http
      .expectOne((r) => r.url === '/api/ledger/movements')
      .flush({ moneyIn: 0, moneyOut: 0, net: 0, months: [], lines: [] });
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
    http
      .expectOne((r) => r.url === '/api/ledger/movements')
      .flush({ moneyIn: 0, moneyOut: 400, net: -400, months: [], lines: [] });
    // The transfers store follows the same notification.
    http.expectOne('/api/payments/transfers/t1').flush(transfer());
  });
});
