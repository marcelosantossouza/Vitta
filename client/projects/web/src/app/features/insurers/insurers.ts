import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { InsurersApi } from '@vitta/core';
import { catchError, of } from 'rxjs';

@Component({
  selector: 'vt-insurers',
  template: `
    <h1>Seguradoras</h1>
    @if (insurers(); as list) {
      <div class="grid">
        @for (insurer of list; track insurer.id) {
          <div class="card"><strong>{{ insurer.code }}</strong><div class="muted">{{ insurer.name }}</div></div>
        } @empty {
          <p class="muted">Nenhuma seguradora (a API está rodando?).</p>
        }
      </div>
    } @else {
      <p class="muted">Carregando…</p>
    }
  `,
})
export class Insurers {
  protected readonly insurers = toSignal(inject(InsurersApi).list().pipe(catchError(() => of([]))));
}
