import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Client, ClientsApi } from '@vitta/core';
import { debounceTime, distinctUntilChanged, Subject, switchMap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'vt-client-list',
  imports: [RouterLink],
  templateUrl: './client-list.html',
})
export class ClientList {
  private readonly api = inject(ClientsApi);
  private readonly search$ = new Subject<string>();

  protected readonly clients = signal<Client[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.search$
      .pipe(debounceTime(300), distinctUntilChanged(), switchMap(term => this.api.list(term)), takeUntilDestroyed())
      .subscribe({
        next: result => {
          this.clients.set(result.items);
          this.total.set(result.totalCount);
          this.loading.set(false);
          this.error.set(null);
        },
        error: () => {
          this.loading.set(false);
          this.error.set('Não foi possível carregar os clientes. A API está rodando?');
        },
      });
    this.search$.next('');
  }

  protected onSearch(term: string): void {
    this.search$.next(term);
  }
}
