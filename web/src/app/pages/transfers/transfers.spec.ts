import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TransfersStore } from '../../banking/transfers.store';
import { OLIVIA, fakeAuth, fakeRealtime, transfer } from '../../testing';
import { Transfers } from './transfers';

describe('Transfers', () => {
  function render(user = fakeAuth()) {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        user.provider,
        fakeRealtime().provider,
      ],
    });
    const store = TestBed.inject(TransfersStore);
    for (const status of ['Completed', 'PendingReview', 'Failed', 'Capturing'] as const) {
      store.upsert(transfer({ id: status, status, toName: status }));
    }
    const fixture = TestBed.createComponent(Transfers);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  const rows = (element: HTMLElement) =>
    Array.from(element.querySelectorAll('tbody strong')).map((cell) => cell.textContent);

  it('filters by outcome, with counts', () => {
    const { fixture, element } = render();
    expect(rows(element)).toHaveLength(4);

    const tabs = element.querySelectorAll<HTMLButtonElement>('[role=tab]');
    expect(tabs[1].textContent).toContain('2');
    tabs[1].click();
    fixture.detectChanges();
    expect(rows(element).sort()).toEqual(['Capturing', 'PendingReview']);

    tabs[4].click();
    fixture.detectChanges();
    expect(rows(element)).toEqual(['Failed']);
  });

  it('offers a new transfer to customers only', () => {
    expect(render().element.querySelector('a.primary')).not.toBeNull();
    TestBed.resetTestingModule();
    const { element } = render(fakeAuth(OLIVIA));
    expect(element.querySelector('a.primary')).toBeNull();
    expect(element.querySelector('h1')!.textContent).toContain('Tous les virements');
  });
});
