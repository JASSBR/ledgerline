import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Account } from '../../banking/models';
import { TransfersStore } from '../../banking/transfers.store';
import { fakeAuth, fakeRealtime, settle, transfer } from '../../testing';
import { TransferNew } from './transfer-new';

const ACCOUNTS: Account[] = [
  {
    id: 'acc-1',
    iban: 'FR7699999000011000000000162',
    ibanFormatted: 'FR76 9999 9000 0110 0000 0000 162',
    name: 'Alice Martin — Compte courant',
    kind: 'Customer',
    balance: 6400,
    available: 6400,
    held: 0,
    holds: [],
    frozen: false,
    frozenReason: null,
  },
];
const BOB = 'FR7699999000012000000000112';

describe('TransferNew', () => {
  let http: HttpTestingController;

  async function render() {
    const fixture = TestBed.createComponent(TransferNew);
    fixture.detectChanges();
    http.expectOne('/api/ledger/accounts').flush(ACCOUNTS);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const type = (selector: string, value: string) => {
      const input = element.querySelector<HTMLInputElement>(selector)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
    };
    const submit = async () => {
      element.querySelector('form')!.dispatchEvent(new Event('submit'));
      await settle();
      fixture.detectChanges();
    };
    return { fixture, element, type, submit };
  }

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

  it('pre-selects the first account and refuses an invalid IBAN before calling the API', async () => {
    const { element, type, submit } = await render();
    expect(element.querySelector('select')!.value).toBe('acc-1');

    type('input.iban', 'FR7699999000012000000000113');
    type('input[type=number]', '50');
    await submit();

    expect(element.textContent).toContain('IBAN invalide');
    http.expectNone('/api/payments/transfers');
  });

  it('confirms who owns the IBAN before paying (verification of payee)', async () => {
    const { element, fixture } = await render();
    (element.querySelector('[data-beneficiary="Bob Durand"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    await settle();

    const lookup = http.expectOne((request) => request.url === '/api/ledger/accounts/lookup');
    expect(lookup.request.params.get('iban')).toBe(BOB);
    lookup.flush({ iban: BOB, name: 'Bob Durand — Compte courant', exists: true });
    await settle();
    fixture.detectChanges();

    expect(element.querySelector('.payee.ok')!.textContent).toContain('Bob Durand');
  });

  it('warns when the amount exceeds the available balance', async () => {
    const { element, type } = await render();
    type('input[type=number]', '7000');
    expect(element.querySelector('.warning')).not.toBeNull();
  });

  it('sends one idempotency key per transfer, and keeps it when the same form is resubmitted', async () => {
    const { type, submit, fixture } = await render();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    type('input.iban', 'fr76 9999 9000 0120 0000 0000 112');
    type('input[type=number]', '42.5');
    type('input:not(.iban):not([type=number])', 'Dîner');
    http
      .match(() => true)
      .filter((request) => !request.cancelled)
      .forEach((request) => request.flush({ iban: BOB, name: 'Bob', exists: true }));

    await submit();
    const first = http.expectOne('/api/payments/transfers');
    expect(first.request.body).toEqual({
      fromAccountId: 'acc-1',
      toIban: BOB,
      amount: 42.5,
      label: 'Dîner',
    });
    const key = first.request.headers.get('Idempotency-Key');
    expect(key).toMatch(/^[0-9a-f-]{36}$/);
    first.flush(null, { status: 0, statusText: 'Network error' });
    await settle();

    await submit();
    const retry = http.expectOne('/api/payments/transfers');
    expect(retry.request.headers.get('Idempotency-Key')).toBe(key);
    retry.flush(transfer({ id: 'created' }));
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith(['/transfers', 'created']);
    expect(TestBed.inject(TransfersStore).transfers()[0].id).toBe('created');
  });

  it('words business rules only the server knows about', async () => {
    const { element, type, submit } = await render();
    type('input.iban', 'FR7699999000011000000000162');
    type('input[type=number]', '10');
    http
      .match(() => true)
      .filter((request) => !request.cancelled)
      .forEach((request) => request.flush({ iban: '', name: '', exists: true }));
    await submit();

    http
      .expectOne('/api/payments/transfers')
      .flush(
        { errors: { 'payments.same_account': ['x'] } },
        { status: 400, statusText: 'Bad Request' },
      );
    await settle();
    TestBed.tick();

    expect(element.querySelector('.alert')!.textContent).toContain(
      'Le bénéficiaire est le compte débité',
    );
  });
});
