import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  DEFAULT_CURRENCY_CODE,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter, withComponentInputBinding, withViewTransitions } from '@angular/router';
import { routes } from './app.routes';
import { apiBaseUrlInterceptor } from './core/api-base-url';
import { Auth } from './core/auth/auth';
import { authInterceptor } from './core/auth/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // The stored OIDC session is read before the first navigation, so guards see the real state.
    provideAppInitializer(() => inject(Auth).restore()),
    provideRouter(routes, withComponentInputBinding(), withViewTransitions()),
    // Order matters: the auth interceptor recognises relative "/api/" URLs before the base URL is prepended.
    provideHttpClient(withFetch(), withInterceptors([authInterceptor, apiBaseUrlInterceptor])),
    { provide: DEFAULT_CURRENCY_CODE, useValue: 'EUR' },
  ],
};
