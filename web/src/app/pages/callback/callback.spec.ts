import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Auth } from '../../core/auth/auth';
import { settle } from '../../testing';
import { Callback } from './callback';

describe('Callback', () => {
  function setup(completeLogin: () => Promise<string>, operator = false) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: Auth, useValue: { completeLogin, isOperator: () => operator } },
      ],
    });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const fixture = TestBed.createComponent(Callback);
    fixture.detectChanges();
    return { fixture, navigate };
  }

  it("sends the user to their role's home, or back where they were", async () => {
    const { navigate } = setup(async () => '/', true);
    await settle();
    expect(navigate).toHaveBeenCalledWith('/ops/reviews', { replaceUrl: true });

    TestBed.resetTestingModule();
    const again = setup(async () => '/transfers/t1');
    await settle();
    expect(again.navigate).toHaveBeenCalledWith('/transfers/t1', { replaceUrl: true });
  });

  it('offers to start again when the code exchange fails', async () => {
    const { fixture } = setup(async () => {
      throw new Error('invalid_grant');
    });
    await settle();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('.alert')).not.toBeNull();
  });
});
