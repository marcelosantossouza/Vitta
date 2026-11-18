import { InjectionToken, Provider } from '@angular/core';

/** URL base da Vitta.API. Web usa '/api' (proxy); o app Ionic usara a URL absoluta. */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  factory: () => '/api',
});

export function provideVittaApi(baseUrl: string): Provider {
  return { provide: API_BASE_URL, useValue: baseUrl };
}
