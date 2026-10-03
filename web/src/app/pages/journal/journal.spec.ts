import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { fakeRealtime } from '../../testing';
import { Journal } from './journal';

describe('Journal', () => {
  it('shows each entry as debit and credit lines', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        fakeRealtime().provider,
      ],
    });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(Journal);
    fixture.detectChanges();

    const request = http.expectOne((r) => r.url === '/api/ledger/journal');
    expect(request.request.params.get('limit')).toBe('100');
    request.flush([
      {
        id: 'e1',
        reference: 'Loyer',
        postedAt: '2026-10-01T10:00:00Z',
        balanced: true,
        lines: [
          { accountId: 'alice', accountName: 'Alice', debit: 900, credit: 0 },
          { accountId: 'bob', accountName: 'Bob', debit: 0, credit: 900 },
        ],
      },
    ]);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('.entry')!.textContent).toContain('Loyer');
    expect(element.querySelector('.chip.ok')).not.toBeNull();
    expect(element.querySelector('.credit-line')!.textContent).toContain('Bob');
    http.verify();
  });
});
