import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import {
  InsurerDto, InsurersApi, PagedResult, PoliciesApi, PolicyDto, PolicyFilter, PolicyStatus, POLICY_STATUS_LABELS,
} from '@vitta/core';
import { debounceTime, Subject, switchMap } from 'rxjs';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { BrlPipe, IsoDatePipe, PolicyStatusPipe } from '../../shared/pipes';
import { ToastService } from '../../shared/toast.service';

@Component({
  selector: 'vt-policy-list',
  imports: [RouterLink, Icon, EmptyState, BrlPipe, IsoDatePipe, PolicyStatusPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './policy-list.html',
})
export class PolicyList {
  private readonly api = inject(PoliciesApi);
  private readonly toast = inject(ToastService);

  private readonly filter$ = new Subject<{ debounce: boolean }>();
  protected readonly filter: Required<Pick<PolicyFilter, 'status' | 'insurerId' | 'search' | 'page'>> = {
    status: 'Active',
    insurerId: '',
    search: '',
    page: 1,
  };

  protected readonly statuses = Object.entries(POLICY_STATUS_LABELS) as [PolicyStatus, string][];
  protected readonly insurers = signal<InsurerDto[]>([]);
  protected readonly result = signal<PagedResult<PolicyDto> | null>(null);
  protected readonly loading = signal(true);

  constructor() {
    inject(InsurersApi).list().subscribe({ next: list => this.insurers.set(list) });

    this.filter$
      .pipe(
        debounceTime(250),
        switchMap(() => {
          this.loading.set(true);
          return this.api.list({ ...this.filter, pageSize: 20 });
        }),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: r => {
          this.result.set(r);
          this.loading.set(false);
        },
        error: err => {
          this.loading.set(false);
          this.toast.error(err, 'Não foi possível carregar as apólices.');
        },
      });

    this.filter$.next({ debounce: false });
  }

  protected setFilter(key: 'status' | 'insurerId' | 'search', value: string): void {
    (this.filter as Record<string, unknown>)[key] = value;
    this.filter.page = 1;
    this.filter$.next({ debounce: key === 'search' });
  }

  protected goTo(page: number): void {
    this.filter.page = page;
    this.filter$.next({ debounce: false });
  }

  protected statusClass(status: string): string {
    return status === 'Active' ? 'badge--success' : status === 'BrokerChanged' ? 'badge--danger' : 'badge--warning';
  }
}
