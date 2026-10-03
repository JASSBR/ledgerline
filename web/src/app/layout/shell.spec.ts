import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { OLIVIA, fakeAuth, fakeRealtime, settle, transfer } from '../testing';
import { Shell } from './shell';

describe('Shell', () => {
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
    const fixture = TestBed.createComponent(Shell);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    return { fixture, http, element: fixture.nativeElement as HTMLElement, user };
  }

  it('gives customers their banking menu and counts transfers in flight', async () => {
    const { fixture, http, element } = render();
    http
      .expectOne('/api/payments/transfers')
      .flush([transfer(), transfer({ id: 't2', status: 'Completed' })]);
    await settle();
    fixture.detectChanges();

    const links = Array.from(element.querySelectorAll('nav a')).map((a) => a.textContent?.trim());
    expect(links[0]).toContain('Mes comptes');
    expect(element.querySelector('nav .count')!.textContent).toBe('1');
    expect(element.textContent).toContain('Client particulier');
  });

  it('gives operators the review queue and the books', async () => {
    const { fixture, http, element, user } = render(fakeAuth(OLIVIA));
    http.expectOne('/api/payments/transfers').flush([transfer({ status: 'PendingReview' })]);
    await settle();
    fixture.detectChanges();

    expect(element.querySelector('nav')!.textContent).toContain('Balance générale');
    expect(element.querySelector('nav .count')!.textContent).toBe('1');

    (element.querySelector('.user') as HTMLButtonElement).click();
    fixture.detectChanges();
    (element.querySelector('.logout') as HTMLButtonElement).click();
    expect(user.fake.logout).toHaveBeenCalled();
  });
});
