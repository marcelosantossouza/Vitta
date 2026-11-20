import { InjectionToken, Provider } from '@angular/core';

/** URL base da Vitta.API. Web usa '/api' (proxy); o app Ionic usara a URL absoluta. */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  factory: () => '/api',
});

export function provideVittaApi(baseUrl: string): Provider {
  return { provide: API_BASE_URL, useValue: baseUrl };
}

/** Rotas usadas pela autenticacao (login e pagina inicial apos entrar). Cada app pode sobrescrever. */
export interface AuthRoutes {
  login: string;
  home: string;
}

export const AUTH_ROUTES = new InjectionToken<AuthRoutes>('AUTH_ROUTES', {
  factory: () => ({ login: '/entrar', home: '/painel' }),
});

export function provideAuthRoutes(routes: AuthRoutes): Provider {
  return { provide: AUTH_ROUTES, useValue: routes };
}
