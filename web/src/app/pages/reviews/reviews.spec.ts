import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Screening } from '../../banking/models';
import { ToastService } from '../../core/toast';
import { OLIVIA, fakeAuth, fakeRealtime, settle } from '../../testing';
import { Reviews } from './reviews';

const HELD: Screening = {
  transferId: 't1',
  fromAccount: 'Alice Martin — Compte courant',
  toIban: 'FR7699999000012000000000112',
  toName: 'Bob Durand — Compte courant',
  amount: 4000,
  requestedAt: '2026-10-02T10:00:00Z',
  decision: 'Review',
  status: 'PendingReview',
  hits: [
    { rule: 'large-amount', outcome: 'Review', explanation: 'Large amount (≥ 3,000 €).' },
    { rule: 'new-beneficiary', outcome: 'Review', explanation: 'First payment.' },
  ],
  decidedBy: null,
  decidedAt: null,
  comment: null,
};

describe('Reviews', () => {
  let http: HttpTestingController;

  const queue = () =>
    http.expectOne(
      (request) =>
        request.url === '/api/fraud/screenings' && request.params.get('status') === 'PendingReview',
    );
  const history = () =>
    http.expectOne(
      (request) => request.url === '/api/fraud/screenings' && !request.params.has('status'),
    );

  async function render(pending: Screening[] = [HELD]) {
    const fixture = TestBed.createComponent(Reviews);
    fixture.detectChanges();
    queue().flush(pending);
    history().flush([
      ...pending,
      { ...HELD, transferId: 't0', status: 'ApprovedByAnalyst', decidedBy: 'olivia', hits: [] },
    ]);
    await fixture.whenStable();
    fixture.detectChanges();
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

  it('shows each held transfer with the rules it hit, named in the UI language', async () => {
    const { element } = await render();
    const card = element.querySelector('[data-transfer="t1"]')!;
    expect(card.textContent).toContain('Montant élevé');
    expect(card.textContent).toContain('Nouveau bénéficiaire');
    expect(element.querySelector('.history')!.textContent).toContain('Approuvé par un analyste');
  });

  it('approves with the optional comment, then refreshes queue and history', async () => {
    const { element, fixture } = await render();
    const textarea = element.querySelector('textarea')!;
    textarea.value = 'Client joint par téléphone';
    textarea.dispatchEvent(new Event('input'));
    (element.querySelector('button.success') as HTMLButtonElement).click();

    const decision: TestRequest = http.expectOne('/api/fraud/screenings/t1/decision');
    expect(decision.request.body).toEqual({ approve: true, comment: 'Client joint par téléphone' });
    decision.flush(null);
    await settle();
    queue().flush([]);
    history().flush([]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(TestBed.inject(ToastService).toasts()[0].tone).toBe('success');
    expect(element.querySelector('.empty')).not.toBeNull();
  });

  it('explains why a rejection was refused', async () => {
    const { element, fixture } = await render();
    (element.querySelector('button.danger') as HTMLButtonElement).click();

    const decision = http.expectOne('/api/fraud/screenings/t1/decision');
    expect(decision.request.body).toEqual({ approve: false, comment: null });
    decision.flush(
      { errors: { 'fraud.comment_required': ['A comment is required.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await settle();
    fixture.detectChanges();

    expect(element.querySelector('.alert')!.textContent).toContain(
      'Un commentaire est obligatoire',
    );
  });
});
