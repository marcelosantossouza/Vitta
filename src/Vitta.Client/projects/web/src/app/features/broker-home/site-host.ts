import { inject } from '@angular/core';
import { CanMatchFn } from '@angular/router';
import { PublicApi } from '@vitta/core';
import { catchError, map, of } from 'rxjs';

/** A raiz (/) mostra a home da corretora quando o site e acessado pelo endereco configurado nela. */
export const siteHostMatch: CanMatchFn = () =>
  inject(PublicApi).site().pipe(
    map(s => s.enabled && !!s.host && s.host === location.hostname.toLowerCase()),
    catchError(() => of(false)),
  );
