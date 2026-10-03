import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { Auth } from './auth';

/** Adds the bearer token to API calls only (never to Keycloak or third parties); a 401 means the session is gone. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(Auth);
  const token = auth.accessToken();
  const isApiCall = request.url.startsWith('/api/');
  const authorized =
    token && isApiCall
      ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : request;

  return next(authorized).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && isApiCall) {
        void auth.login(undefined, globalThis.location?.pathname ?? '/');
      }
      return throwError(() => error);
    }),
  );
};
