import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { AuthService } from './auth.service';

/** Adiciona o Bearer token nas chamadas da API e desloga em caso de 401. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const baseUrl = inject(API_BASE_URL);
  const isApiCall = req.url.startsWith(baseUrl);
  const isLogin = req.url.endsWith('/auth/login');
  const token = isApiCall && !isLogin ? auth.token : null;

  const request = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && isApiCall && !isLogin) {
        auth.logout();
      }
      return throwError(() => error);
    }),
  );
};
