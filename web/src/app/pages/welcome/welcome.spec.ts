import { TestBed } from '@angular/core/testing';
import { fakeAuth } from '../../testing';
import { Welcome } from './welcome';

describe('Welcome', () => {
  it('signs in through Keycloak with the chosen demo username pre-filled', async () => {
    const auth = fakeAuth(null);
    TestBed.configureTestingModule({ providers: [auth.provider] });
    const fixture = TestBed.createComponent(Welcome);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelectorAll('.persona')).toHaveLength(4);
    expect(element.querySelector('.tip code')!.textContent).toBe('ledgerline-demo');

    (element.querySelector('[data-user="olivia"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(auth.fake.login).toHaveBeenCalledWith('olivia');
    expect(element.querySelector('[data-user="olivia"] .spinner')).not.toBeNull();
    expect(element.querySelector<HTMLButtonElement>('[data-user="alice"]')!.disabled).toBe(true);
  });
});
