import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Auth } from './auth';

/** Where each role lands after signing in. */
export function homeFor(isOperator: boolean): string {
  return isOperator ? '/ops/reviews' : '/accounts';
}

export const guestGuard: CanActivateFn = () => {
  const auth = inject(Auth);
  return !auth.isAuthenticated() || inject(Router).createUrlTree([homeFor(auth.isOperator())]);
};

export const signedInGuard: CanActivateFn = () =>
  inject(Auth).isAuthenticated() || inject(Router).createUrlTree(['/']);

export const operatorGuard: CanActivateFn = () =>
  inject(Auth).isOperator() || inject(Router).createUrlTree(['/accounts']);

export const customerGuard: CanActivateFn = () =>
  !inject(Auth).isOperator() || inject(Router).createUrlTree(['/ops/reviews']);
