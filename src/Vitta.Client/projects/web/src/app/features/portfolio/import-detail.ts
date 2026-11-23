import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { normalizeText, PortfolioApi, PortfolioChangeType, PortfolioImportDetailDto, PortfolioRowDto } from '@vitta/core';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { BrlPipe, ChangeTypePipe, CpfPipe, IsoDatePipe, MonthPipe } from '../../shared/pipes';
import { ToastService } from '../../shared/toast.service';

type Tab = PortfolioChangeType | 'all';
type View = 'changes' | 'rows';

const PAGE_SIZE = 100;
const percent = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 });

@Component({
  selector: 'vt-import-detail',
  imports: [RouterLink, Icon, EmptyState, MonthPipe, ChangeTypePipe, CpfPipe, IsoDatePipe, BrlPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './import-detail.html',
  styles: `
    .summary { display: grid; gap: 1rem; grid-template-columns: repeat(auto-fit, minmax(min(100%, 170px), 1fr)); }
    .stat { display: flex; flex-direction: column; gap: 0.2rem;
      strong { font-size: 1.6rem; font-weight: 750; } span { color: var(--vt-muted); font-size: 0.85rem; } }
    .stat--success strong { color: var(--vt-success); }
    .stat--danger strong { color: var(--vt-danger); }
    .stat--warning strong { color: var(--vt-warning); }
    .view-tabs { align-self: flex-start; }
    .rows-toolbar { display: flex; flex-wrap: wrap; gap: 0.6rem 1rem; align-items: center; justify-content: space-between; width: 100%;
      .search { flex: 1 1 260px; max-width: 420px; } }
    .totals { display: flex; flex-wrap: wrap; gap: 0.25rem 1rem; font-size: 0.875rem; color: var(--vt-muted);
      strong { color: var(--vt-text); font-variant-numeric: tabular-nums; } }
    .product { display: inline-flex; flex-wrap: wrap; gap: 0.3rem 0.5rem; align-items: center; justify-content: flex-end; }
    .cell-sub { display: block; font-size: 0.8rem; color: var(--vt-muted); }
    td.wrap-anywhere { overflow-wrap: break-word; min-width: 7rem; }
    tr.extras-row { background: var(--vt-surface-2); }
    tr.extras-row td { text-align: left; padding: 0.6rem 1rem 0.8rem; }
    @media (max-width: 720px) { tr.extras-row td { display: block; } }
    tr.extras-row td::before { content: none; }
    .extras { margin: 0; display: grid; gap: 0.25rem 1.25rem; grid-template-columns: repeat(auto-fill, minmax(min(100%, 240px), 1fr));
      font-size: 0.85rem; overflow-wrap: anywhere; }
    .extras span { color: var(--vt-muted); }
    .sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
    .more { display: flex; justify-content: center; padding: 0.8rem 1rem; border-top: 1px solid var(--vt-border); }
  `,
})
export class ImportDetail implements OnInit {
  private readonly api = inject(PortfolioApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  readonly id = input.required<string>();

  protected readonly data = signal<PortfolioImportDetailDto | null>(null);
  protected readonly tab = signal<Tab>('Left');

  protected readonly view = signal<View>('changes');
  protected readonly rows = signal<PortfolioRowDto[] | null>(null);
  protected readonly rowsLoading = signal(false);
  protected readonly rowsSearch = signal('');
  protected readonly rowsLimit = signal(PAGE_SIZE);
  protected readonly expanded = signal<ReadonlySet<string>>(new Set());

  /** Colunas exibidas: some quando nenhuma linha tem valor (a origem pode nao ter o dado). */
  protected readonly cols = computed(() => {
    const list = this.rows() ?? [];
    const has = (pick: (r: PortfolioRowDto) => unknown) => list.some(r => { const v = pick(r); return v !== null && v !== undefined && v !== ''; });
    return {
      cpf: has(r => r.holderCpf),
      product: has(r => r.productName),
      premium: has(r => r.monthlyPremium),
      commission: has(r => r.commissionAmount) || has(r => r.commissionRate),
      installment: has(r => r.installment),
      status: has(r => r.status),
      start: has(r => r.startDate),
      city: has(r => r.city),
      extras: has(r => r.extras),
    };
  });

  protected readonly colCount = computed(() => 2 + Object.values(this.cols()).filter(Boolean).length);

  protected readonly filteredRows = computed(() => {
    const list = this.rows() ?? [];
    const term = normalizeText(this.rowsSearch());
    if (!term) return list;
    const digits = term.replace(/\D/g, '');
    return list.filter(r =>
      normalizeText(r.holderName).includes(term) ||
      normalizeText(r.displayId).includes(term) ||
      (digits.length >= 3 && (r.holderCpf ?? '').includes(digits)));
  });

  protected readonly visibleRows = computed(() => this.filteredRows().slice(0, this.rowsLimit()));

  protected readonly totals = computed(() => {
    const list = this.filteredRows();
    const sum = (pick: (r: PortfolioRowDto) => number | null) => list.reduce((acc, r) => acc + (pick(r) ?? 0), 0);
    return { count: list.length, premium: sum(r => r.monthlyPremium), commission: sum(r => r.commissionAmount) };
  });

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
        // Primeira importacao nao tem mudancas: mostra direto as apolices do mes.
        if (d.isFirstImport) this.showView('rows');
        // Abre na aba mais relevante: saidas, se houver.
        if (d.left === 0) this.tab.set(d.entered > 0 ? 'Entered' : d.changed > 0 ? 'Changed' : 'all');
      },
      error: err => {
        this.toast.error(err, 'Importação não encontrada.');
        void this.router.navigate(['/carteira']);
      },
    });
  }

  protected showView(view: View): void {
    this.view.set(view);
    if (view === 'rows' && this.rows() === null && !this.rowsLoading()) this.loadRows();
  }

  protected onRowsSearch(value: string): void {
    this.rowsSearch.set(value);
    this.rowsLimit.set(PAGE_SIZE);
  }

  protected showMoreRows(): void {
    this.rowsLimit.update(n => n + PAGE_SIZE);
  }

  protected toggleExtras(id: string): void {
    this.expanded.update(set => {
      const next = new Set(set);
      if (!next.delete(id)) next.add(id);
      return next;
    });
  }

  /** "Coluna: valor" por linha -> pares para exibir. */
  protected extrasLines(extras: string | null): { label: string; value: string }[] {
    return (extras ?? '').split('\n').map(l => l.trim()).filter(Boolean).map(line => {
      const i = line.indexOf(':');
      return i > 0 ? { label: line.slice(0, i).trim(), value: line.slice(i + 1).trim() } : { label: '', value: line };
    });
  }

  protected rate(value: number): string {
    return `${percent.format(value)}%`;
  }

  private loadRows(): void {
    this.rowsLoading.set(true);
    this.api.rows(this.id()).subscribe({
      next: rows => {
        this.rows.set(rows);
        this.rowsLoading.set(false);
      },
      error: err => {
        this.rowsLoading.set(false);
        this.toast.error(err, 'Não foi possível carregar as apólices do mês.');
      },
    });
  }

  protected badgeClass(type: PortfolioChangeType): string {
    return type === 'Left' ? 'badge--danger' : type === 'Entered' ? 'badge--success' : 'badge--warning';
  }
}
