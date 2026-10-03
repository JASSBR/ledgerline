import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TrialBalance as Report } from '../../banking/models';
import { OLIVIA, fakeAuth, fakeRealtime, settle } from '../../testing';
import { TrialBalance } from './trial-balance';

const REPORT: Report = {
  accounts: [
    { accountId: 'treasury', name: 'Treasury', kind: 'Internal', balance: -22740 },
    { accountId: 'alice', name: 'Alice', kind: 'Customer', balance: 18400 },
    { accountId: 'bob', name: 'Bob', kind: 'Customer', balance: 4340 },
  ],
  total: 0,
  balanced: true,
  journalEntries: 12,
};

describe('TrialBalance', () => {
  let http: HttpTestingController;

  async function render(report = REPORT) {
    const fixture = TestBed.createComponent(TrialBalance);
    fixture.detectChanges();
    http.expectOne('/api/ledger/trial-balance').flush(report);
    await fixture.whenStable();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        fakeAuth(OLIVIA).provider,
        fakeRealtime().provider,
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('proves the books sum to zero', async () => {
    const { element } = await render();
    expect(element.querySelector('.proof.ok')!.textContent).toContain('équilibrés');
    expect(element.querySelectorAll('tbody tr')).toHaveLength(3);
  });

  it('flags unbalanced books', async () => {
    const { element } = await render({ ...REPORT, total: 1, balanced: false });
    expect(element.querySelector('.proof.ok')).toBeNull();
    expect(element.querySelector('.proof')!.textContent).toContain('ne sont pas');
  });

  it('posts a cash deposit to the first customer by default, then reloads', async () => {
    const { element, fixture } = await render();
    element.querySelector('form')!.dispatchEvent(new Event('submit'));

    const deposit = http.expectOne('/api/ledger/accounts/alice/deposits');
    expect(deposit.request.body).toEqual({ amount: 500, reference: 'Dépôt guichet' });
    deposit.flush(null);
    await settle();
    http.expectOne('/api/ledger/trial-balance').flush(REPORT);
    await fixture.whenStable();
  });

  it('shows why a deposit was refused', async () => {
    const { element, fixture } = await render();
    const amount = element.querySelector<HTMLInputElement>('input[type=number]')!;
    amount.value = '-1';
    amount.dispatchEvent(new Event('input'));
    element.querySelector('form')!.dispatchEvent(new Event('submit'));

    http
      .expectOne('/api/ledger/accounts/alice/deposits')
      .flush(
        { errors: { 'ledger.amount_not_positive': ['x'] } },
        { status: 400, statusText: 'Bad Request' },
      );
    await settle();
    fixture.detectChanges();
    expect(element.querySelector('.alert')!.textContent).toContain('strictement positif');
  });
});
