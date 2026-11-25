import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { ClientListItemDto, ClientsApi, PagedResult } from '@vitta/core';
import { debounceTime, distinctUntilChanged, merge, Subject, switchMap, map } from 'rxjs';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { BookKpis } from '../../shared/book-kpis';
import { BrlPipe, PercentPipe } from '../../shared/pipes';
import { ToastService } from '../../shared/toast.service';

const PAGE_SIZE = 20;

@Component({
  selector: 'vt-client-list',
  imports: [RouterLink, Icon, EmptyState, BrlPipe, PercentPipe, BookKpis],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './client-list.html',
})
export class ClientList {
  private readonly api = inject(ClientsApi);
  private readonly toast = inject(ToastService);
  protected readonly router = inject(Router);

  private readonly search$ = new Subject<string>();
  private readonly page$ = new Subject<number>();
  private term = '';

  protected readonly result = signal<PagedResult<ClientListItemDto> | null>(null);
  protected readonly loading = signal(true);
  protected readonly searching = signal(false);

  /** % efetivo do cliente: comissao / premio das apolices ativas. */
  protected rateOf(c: ClientListItemDto): number | null {
    return c.monthlyCommission != null && c.monthlyPremium > 0 ? (c.monthlyCommission / c.monthlyPremium) * 100 : null;
  }

  constructor() {
    const searches = this.search$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      map(term => {
        this.term = term;
        return 1;
      }),
    );

    merge(searches, this.page$)
      .pipe(
        switchMap(page => {
          this.loading.set(true);
          return this.api.list(this.term, page, PAGE_SIZE);
        }),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: r => {
          this.result.set(r);
          this.loading.set(false);
          this.searching.set(this.term.length > 0);
        },
        error: err => {
          this.loading.set(false);
          this.toast.error(err, 'Não foi possível carregar os clientes.');
        },
      });

    this.page$.next(1);
  }

  protected onSearch(term: string): void {
    this.search$.next(term.trim());
  }

  protected goTo(page: number): void {
    this.page$.next(page);
  }
}
