import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AUTH_ROUTES } from '../config/api-config';
import { AuthService } from './auth.service';

/** Rota da troca obrigatoria de senha (senha provisoria definida por um administrador). */
export const CHANGE_PASSWORD_ROUTE = '/trocar-senha';

/** Exige login. Sem sessao, manda para a tela de login guardando a URL de retorno (?voltar=). Com senha provisoria, manda trocar. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  if (auth.isAuthenticated()) {
    return auth.mustChangePassword() && !state.url.startsWith(CHANGE_PASSWORD_ROUTE)
      ? inject(Router).createUrlTree([CHANGE_PASSWORD_ROUTE])
      : true;
  }
  return inject(Router).createUrlTree([inject(AUTH_ROUTES).login], { queryParams: { voltar: state.url } });
};

/** Para telas publicas (login): quem ja esta logado vai direto para o app. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() ? inject(Router).createUrlTree([inject(AUTH_ROUTES).home]) : true;
};

/** So administradores (Usuarios, Configuracoes). Os demais voltam para o painel. */
export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAdmin() ? true : inject(Router).createUrlTree([inject(AUTH_ROUTES).home]);
};
