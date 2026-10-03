import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Movements as MovementsReport } from '../../banking/models';
import { account, fakeAuth, fakeRealtime, settle } from '../../testing';
import { Movements } from './movements';

const REPORT: MovementsReport = {
  moneyIn: 7300,
  moneyOut: 900,
  net: 6400,
  months: [
    { month: '2026-09', moneyIn: 7300, moneyOut: 900 },
    { month: '2026-10', moneyIn: 0, moneyOut: 0 },
  ],
  lines: [
    {
      entryId: 'e2',
      accountId: 'acc-1',
      accountName: 'Alice Martin — Compte courant',
      valueDate: '2026-09-30T00:00:00Z',
      reference: 'Loyer',
      amount: -900,
      counterparty: 'Bob',
    },
    {
      entryId: 'e1',
      accountId: 'acc-2',
      accountName: 'Alice Martin — Livret',
      valueDate: '2026-09-01T00:00:00Z',
      reference: 'Salaire',
      amount: 7300,
      counterparty: 'Trésorerie',
    },
  ],
};

describe('Movements', () => {
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
    const fixture = TestBed.createComponent(Movements);
    fixture.detectChanges();
    http
      .expectOne('/api/ledger/accounts')
      .flush([account(), account({ id: 'acc-2', name: 'Alice Martin — Livret' })]);
    const first = http.expectOne((r) => r.url === '/api/ledger/movements');
    first.flush(REPORT);
    await fixture.whenStable();
    return { fixture, element: fixture.nativeElement as HTMLElement, first };
  }

  it('shows money in, money out and the net, with a bar per month', async () => {
    const { element, first } = await render();

    expect(first.request.params.get('from')).toMatch(/^\d{4}-\d{2}-\d{2}T00:00:00Z$/);
    expect(first.request.params.has('direction')).toBe(false);
    expect(element.querySelector('.total.in')!.textContent).toMatch(/\+7\s?300,00/);
    expect(element.querySelector('.total.out')!.textContent).toMatch(/−900,00/);
    expect(element.querySelector('.total.net')!.textContent).toMatch(/\+6\s?400,00/);
    expect(element.querySelectorAll('app-flow-chart .month')).toHaveLength(2);
    expect(element.querySelectorAll('tbody tr')).toHaveLength(2);
    expect(element.querySelector('td.debit')!.textContent).toContain('900');
  });

  it('asks the server for one direction and one account at a time', async () => {
    const { element, fixture } = await render();

    element.querySelectorAll<HTMLButtonElement>('.segmented button')[2].click();
    await settle();
    const outgoing = http.expectOne((r) => r.url === '/api/ledger/movements');
    expect(outgoing.request.params.get('direction')).toBe('out');
    outgoing.flush({ ...REPORT, lines: [REPORT.lines[0]] });
    await fixture.whenStable();

    const accountSelect = element.querySelectorAll<HTMLSelectElement>('select')[1];
    accountSelect.value = 'acc-2';
    accountSelect.dispatchEvent(new Event('change'));
    await settle();
    const oneAccount = http.expectOne((r) => r.url === '/api/ledger/movements');
    expect(oneAccount.request.params.get('accountId')).toBe('acc-2');
    oneAccount.flush(REPORT);
  });

  it('exports the lines on screen as a CSV file', async () => {
    const createObjectURL = vi.fn((blob: Blob) => (blob ? 'blob:csv' : ''));
    Object.assign(URL, { createObjectURL, revokeObjectURL: vi.fn() });
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, 'click')
      .mockImplementation(() => undefined);
    const { element } = await render();

    element.querySelector<HTMLButtonElement>('.page-header button')!.click();

    const csv = await (createObjectURL.mock.calls[0][0] as Blob).text();
    expect(csv).toContain('Loyer;Bob;-900,00');
    expect(click).toHaveBeenCalled();
  });
});
