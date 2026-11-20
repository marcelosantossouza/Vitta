import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { PortfolioApi, PortfolioChangeType, PortfolioImportDetailDto } from '@vitta/core';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { ChangeTypePipe, CpfPipe, IsoDatePipe, MonthPipe } from '../../shared/pipes';
import { ToastService } from '../../shared/toast.service';

type Tab = PortfolioChangeType | 'all';

@Component({
  selector: 'vt-import-detail',
  imports: [RouterLink, Icon, EmptyState, MonthPipe, ChangeTypePipe, CpfPipe, IsoDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './import-detail.html',
  styles: `
    .summary { display: grid; gap: 1rem; grid-template-columns: repeat(auto-fit, minmax(min(100%, 170px), 1fr)); }
    .stat { display: flex; flex-direction: column; gap: 0.2rem;
      strong { font-size: 1.6rem; font-weight: 750; } span { color: var(--vt-muted); font-size: 0.85rem; } }
    .stat--success strong { color: var(--vt-success); }
    .stat--danger strong { color: var(--vt-danger); }
    .stat--warning strong { color: var(--vt-warning); }
  `,
})
export class ImportDetail implements OnInit {
  private readonly api = inject(PortfolioApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  readonly id = input.required<string>();

  protected readonly data = signal<PortfolioImportDetailDto | null>(null);
  protected readonly tab = signal<Tab>('Left');

  protected readonly tabs = computed(() => {
    const d = this.data();
    return [
      { value: 'Left' as Tab, label: 'Saíram', count: d?.left ?? 0 },
      { value: 'Entered' as Tab, label: 'Entraram', count: d?.entered ?? 0 },
      { value: 'Changed' as Tab, label: 'Mudaram', count: d?.changed ?? 0 },
      { value: 'all' as Tab, label: 'Todas', count: d?.changes.length ?? 0 },
    ];
  });

  protected readonly visible = computed(() => {
    const d = this.data();
    if (!d) return [];
    const tab = this.tab();
    return tab === 'all' ? d.changes : d.changes.filter(c => c.type === tab);
  });

  ngOnInit(): void {
    this.api.get(this.id()).subscribe({
      next: d => {
        this.data.set(d);
        // Abre na aba mais relevante: saidas, se houver.
        if (d.left === 0) this.tab.set(d.entered > 0 ? 'Entered' : d.changed > 0 ? 'Changed' : 'all');
      },
      error: err => {
        this.toast.error(err, 'Importação não encontrada.');
        void this.router.navigate(['/carteira']);
      },
    });
  }

  protected badgeClass(type: PortfolioChangeType): string {
    return type === 'Left' ? 'badge--danger' : type === 'Entered' ? 'badge--success' : 'badge--warning';
  }
}
