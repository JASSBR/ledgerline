import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { fakeRealtime, settle, transfer } from '../testing';
import { TransfersStore } from './transfers.store';

describe('TransfersStore', () => {
  let http: HttpTestingController;

  function setup() {
    const realtime = fakeRealtime();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), realtime.provider],
    });
    http = TestBed.inject(HttpTestingController);
    return { store: TestBed.inject(TransfersStore), realtime: realtime.fake };
  }

  afterEach(() => http.verify());

  it('loads transfers and derives what is in flight or awaiting review', async () => {
    const { store } = setup();
    const loading = store.load();
    expect(store.loading()).toBe(true);
    http
      .expectOne('/api/payments/transfers')
      .flush([
        transfer({ id: 'a', status: 'PendingReview' }),
        transfer({ id: 'b', status: 'Completed' }),
        transfer({ id: 'c', status: 'Capturing' }),
      ]);
    await loading;

    expect(store.loading()).toBe(false);
    expect(store.inFlight().map((t) => t.id)).toEqual(['a', 'c']);
    expect(store.pendingReview().map((t) => t.id)).toEqual(['a']);
  });

  it('reports a failed load', async () => {
    const { store } = setup();
    const loading = store.load();
    http.expectOne('/api/payments/transfers').flush(null, { status: 500, statusText: 'Error' });
    await loading;
    expect(store.error()).toBe(true);
  });

  it('refetches only the transfer a notification is about, patching it in place', async () => {
    const { store, realtime } = setup();
    store.upsert(transfer({ id: 'b' }));
    store.upsert(transfer({ id: 'a', status: 'Screening' }));

    realtime.lastChange.set({
      transferId: 'a',
      ownerId: 'x',
      status: 'Completed',
      reason: null,
      occurredAt: '',
    });
    TestBed.tick();
    http.expectOne('/api/payments/transfers/a').flush(transfer({ id: 'a', status: 'Completed' }));
    await settle();

    expect(store.transfers().map((t) => [t.id, t.status])).toEqual([
      ['a', 'Completed'],
      ['b', 'Screening'],
    ]);
  });

  it('prepends a transfer it did not know (made in another tab)', async () => {
    const { store } = setup();
    store.upsert(transfer({ id: 'old' }));
    const refresh = store.refresh('new');
    http.expectOne('/api/payments/transfers/new').flush(transfer({ id: 'new' }));
    await refresh;
    expect(store.transfers().map((t) => t.id)).toEqual(['new', 'old']);
  });
});
