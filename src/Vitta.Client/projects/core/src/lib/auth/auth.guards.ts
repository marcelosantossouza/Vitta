import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AUTH_ROUTES } from '../config/api-config';
import { AuthService } from './auth.service';

/** Exige login. Sem sessao, manda para a tela de login guardando a URL de retorno (?voltar=). */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  if (auth.isAuthenticated()) {
    return true;
  }
  return inject(Router).createUrlTree([inject(AUTH_ROUTES).login], { queryParams: { voltar: state.url } });
};

/** Para telas publicas (login): quem ja esta logado vai direto para o app. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() ? inject(Router).createUrlTree([inject(AUTH_ROUTES).home]) : true;
};
