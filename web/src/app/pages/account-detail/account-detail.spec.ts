import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { account, fakeAuth, fakeRealtime, settle } from '../../testing';
import { AccountDetail } from './account-detail';

describe('AccountDetail', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        fakeAuth().provider,
        fakeRealtime().provider,
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render() {
    const fixture = TestBed.createComponent(AccountDetail);
    fixture.componentRef.setInput('id', 'acc-1');
    fixture.detectChanges();
    http.expectOne('/api/ledger/accounts/acc-1').flush(
      account({
        held: 50,
        available: 6350,
        holds: [{ transferId: 't9', amount: 50, reference: 'Cinéma' }],
      }),
    );
    http
      .expectOne((request) => request.url === '/api/ledger/accounts/acc-1/statement')
      .flush([
        {
          entryId: 'e2',
          valueDate: '2026-09-30T00:00:00Z',
          reference: 'Loyer',
          amount: -900,
          counterparty: 'Bob',
          balanceAfter: 6400,
        },
        {
          entryId: 'e1',
          valueDate: '2026-09-01T00:00:00Z',
          reference: 'Salaire',
          amount: 7300,
          counterparty: 'Treasury',
          balanceAfter: 7300,
        },
      ]);
    await fixture.whenStable();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  it('shows balances, holds, the statement and a balance chart', async () => {
    const { element } = await render();
    expect(element.querySelector('h1')!.textContent).toContain('Compte courant');
    expect(element.querySelector('.holds')!.textContent).toContain('Cinéma');
    expect(element.querySelectorAll('tbody tr')).toHaveLength(2);
    expect(element.querySelector('td.credit')!.textContent).toContain('+');
    expect(element.querySelector('app-balance-chart path')).not.toBeNull();
  });

  it('replays the balance at a past value date, including that whole day', async () => {
    const { element, fixture } = await render();
    const date = element.querySelector<HTMLInputElement>('#as-of')!;
    date.value = '2026-09-15';
    date.dispatchEvent(new Event('change'));
    await settle();

    const request = http.expectOne((r) => r.url === '/api/ledger/accounts/acc-1/balance');
    expect(request.request.params.get('asOf')).toBe('2026-09-15T23:59:59Z');
    request.flush({
      accountId: 'acc-1',
      asOf: '2026-09-15T23:59:59Z',
      balance: 7300,
      available: 7300,
    });
    await fixture.whenStable();

    expect(element.querySelector('.as-of strong')!.textContent).toMatch(/7\s?300,00/);
  });

  it('copies the IBAN', async () => {
    const writeText = vi.fn(async () => undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    const { element } = await render();
    (element.querySelector('.iban-copy') as HTMLButtonElement).click();
    expect(writeText).toHaveBeenCalledWith('FR7699999000011000000000162');
  });
});
