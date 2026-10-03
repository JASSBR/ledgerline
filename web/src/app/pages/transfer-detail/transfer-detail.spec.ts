import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { fakeAuth, fakeRealtime, settle, transfer } from '../../testing';
import { TransferDetail } from './transfer-detail';

describe('TransferDetail', () => {
  let http: HttpTestingController;
  let realtime: ReturnType<typeof fakeRealtime>['fake'];

  async function render(id = 't1') {
    const fixture = TestBed.createComponent(TransferDetail);
    fixture.componentRef.setInput('id', id);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

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

  it('renders the saga with the service doing each step, and follows it live', async () => {
    const { fixture, element } = await render();
    http.expectOne('/api/payments/transfers/t1').flush(transfer());
    await settle();
    fixture.detectChanges();

    const states = () =>
      Array.from(element.querySelectorAll('.stages li')).map((li) => li.getAttribute('data-state'));
    expect(states()).toEqual(['done', 'current', 'upcoming', 'upcoming']);
    expect(element.textContent).toContain('Service Fraud');
    expect(element.querySelector('.chip.live')).not.toBeNull();

    realtime.lastChange.set({
      transferId: 't1',
      ownerId: 'x',
      status: 'Completed',
      reason: null,
      occurredAt: '',
    });
    TestBed.tick();
    http.expectOne('/api/payments/transfers/t1').flush(
      transfer({
        status: 'Completed',
        requestedAt: '2026-10-02T10:00:00Z',
        steps: [
          { status: 'Reserving', at: '2026-10-02T10:00:00.1Z', detail: null },
          { status: 'Screening', at: '2026-10-02T10:00:00.3Z', detail: null },
          { status: 'Capturing', at: '2026-10-02T10:00:00.6Z', detail: null },
          { status: 'Completed', at: '2026-10-02T10:00:00.9Z', detail: null },
        ],
      }),
    );
    await settle();
    fixture.detectChanges();

    expect(states()).toEqual(['done', 'done', 'done', 'done']);
    expect(element.textContent).toContain('0.9');
  });

  it('says so when the transfer does not exist', async () => {
    const { fixture, element } = await render('missing');
    http
      .expectOne('/api/payments/transfers/missing')
      .flush(null, { status: 404, statusText: 'Not Found' });
    await settle();
    fixture.detectChanges();
    expect(element.querySelector('.alert')!.textContent).toContain("n'existe pas");
  });
});
