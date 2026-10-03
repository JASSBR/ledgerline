import { HttpInterceptorFn } from '@angular/common/http';
import { InjectionToken, inject } from '@angular/core';
import { environment } from '../../environments/environment';

export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => environment.apiBaseUrl,
});

/** Code always calls "/api/…"; this is the one place that knows where the API actually lives. */
export const apiBaseUrlInterceptor: HttpInterceptorFn = (request, next) => {
  const baseUrl = inject(API_BASE_URL);
  return baseUrl && request.url.startsWith('/')
    ? next(request.clone({ url: `${baseUrl}${request.url}` }))
    : next(request);
};
