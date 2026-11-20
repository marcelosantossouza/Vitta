import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService, DashboardApi, DashboardDto, PROSPECT_STAGES, todayIso } from '@vitta/core';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { BrlPipe, ChangeTypePipe, IsoDatePipe, MonthPipe, StagePipe } from '../../shared/pipes';
import { ToastService } from '../../shared/toast.service';

@Component({
  selector: 'vt-dashboard',
  imports: [RouterLink, Icon, EmptyState, BrlPipe, IsoDatePipe, StagePipe, MonthPipe, ChangeTypePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard {
  private readonly api = inject(DashboardApi);
  private readonly toast = inject(ToastService);

  protected readonly user = inject(AuthService).currentUser;
  protected readonly data = signal<DashboardDto | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly today = todayIso();

  protected readonly greeting = (() => {
    const hour = new Date().getHours();
    return hour < 12 ? 'Bom dia' : hour < 18 ? 'Boa tarde' : 'Boa noite';
  })();

  protected readonly firstName = computed(() => (this.user()?.name ?? '').split(' ')[0]);

  /** Funil completo (inclui estagios com zero) para o grafico de barras. */
  protected readonly funnel = computed(() => {
    const d = this.data();
    if (!d) return [];
    const counts = new Map(d.prospectsByStage.map(s => [s.stage, s.count]));
    const rows = PROSPECT_STAGES.map(stage => ({ stage, count: counts.get(stage) ?? 0 }));
    const max = Math.max(1, ...rows.map(r => r.count));
    return rows.map(r => ({ ...r, pct: (r.count / max) * 100 }));
  });

  protected readonly insurers = computed(() => {
    const d = this.data();
    if (!d) return [];
    const total = d.policiesByInsurer.reduce((sum, i) => sum + i.monthlyPremium, 0) || 1;
    return [...d.policiesByInsurer]
      .sort((a, b) => b.monthlyPremium - a.monthlyPremium)
      .map(i => ({ ...i, pct: (i.monthlyPremium / total) * 100 }));
  });

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.api.get().subscribe({
      next: d => {
        this.data.set(d);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.failed.set(true);
        this.toast.error(err, 'Não foi possível carregar o painel.');
      },
    });
  }

  protected isOverdue(date: string | null): boolean {
    return !!date && date < this.today;
  }
}
