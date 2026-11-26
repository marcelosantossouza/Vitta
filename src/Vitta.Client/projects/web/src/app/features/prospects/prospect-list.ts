import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import {
  AuthService, FOLLOW_UP_FILTER_LABELS, FollowUpFilter, LabelCount, PagedResult, problemMessage, PROSPECT_SORT_LABELS, PROSPECT_STAGE_LABELS,
  PROSPECT_STAGES, ProspectDto, ProspectFilter, ProspectFilterOptionsDto, ProspectInsightsDto, ProspectsApi, ProspectSort, ProspectStage,
  formatCnpj, formatCpf, LeadKind, todayIso,
} from '@vitta/core';
import { debounceTime, Subject } from 'rxjs';
import { Icon } from '../../shared/icon';
import { mediaQuery, MOBILE_QUERY } from '../../shared/media';
import { Sheet } from '../../shared/sheet';
import { BrlPipe, IsoDatePipe, PercentPipe } from '../../shared/pipes';
import { ToastService } from '../../shared/toast.service';
import { ProspectTabs } from './prospect-tabs';

const PAGE_SIZE = 50;
const OPEN_STAGES: ProspectStage[] = ['New', 'Contacted', 'MeetingScheduled', 'ProposalSent'];

/**
 * Gestao da prospeccao: filtros (cidade, UF, salario, etapa, origem, importacao, contato), dashboard com os mesmos
 * filtros, lista ordenavel e acoes em lote para escolher quem contatar.
 */
@Component({
  selector: 'vt-prospect-list',
  imports: [Icon, BrlPipe, IsoDatePipe, PercentPipe, ProspectTabs, NgTemplateOutlet, Sheet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './prospect-list.html',
  styleUrl: './prospect-list.scss',
})
export class ProspectList {
  private readonly api = inject(ProspectsApi);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  /** Assistentes nao excluem (a API tambem bloqueia). */
  protected readonly canDelete = inject(AuthService).canDelete;

  protected readonly stages = PROSPECT_STAGES;
  protected readonly stageLabels = PROSPECT_STAGE_LABELS;
  protected readonly followUpLabels = FOLLOW_UP_FILTER_LABELS;
  protected readonly followUps = Object.keys(FOLLOW_UP_FILTER_LABELS) as FollowUpFilter[];
  protected readonly sortLabels = PROSPECT_SORT_LABELS;
  protected readonly sorts = Object.keys(PROSPECT_SORT_LABELS) as ProspectSort[];
  protected readonly today = todayIso();

  // ---- filtros ----
  protected readonly isMobile = mediaQuery(MOBILE_QUERY);
  protected readonly filtersOpen = signal(false);
  protected readonly chartsOpen = signal(false);
  protected readonly search = signal('');
  /** CPF ou CNPJ (começo). */
  protected readonly document = signal('');
  protected readonly stageFilter = signal<ProspectStage[]>([...OPEN_STAGES]);
  protected readonly city = signal('');
  protected readonly state = signal('');
  protected readonly source = signal('');
  protected readonly importId = signal('');
  protected readonly salaryMin = signal<number | null>(null);
  protected readonly salaryMax = signal<number | null>(null);
  protected readonly followUp = signal<FollowUpFilter>('Any');
  protected readonly hasPhone = signal<'' | 'yes' | 'no'>('');
  protected readonly kind = signal<LeadKind>('Any');
  protected readonly companySize = signal('');
  protected readonly formatCnpj = formatCnpj;
  protected readonly formatCpf = formatCpf;
  protected readonly sort = signal<ProspectSort>('FollowUp');
  protected readonly page = signal(1);

  protected readonly filter = computed<ProspectFilter>(() => ({
    search: this.search().trim() || undefined,
    document: this.document().replace(/\D/g, '') || undefined,
    stage: this.stageFilter().length === this.stages.length ? undefined : this.stageFilter(),
    city: this.city() || undefined,
    state: this.state() || undefined,
    source: this.source() || undefined,
    importId: this.importId() || undefined,
    salaryMin: this.salaryMin(),
    salaryMax: this.salaryMax(),
    followUp: this.followUp(),
    hasPhone: this.hasPhone() === '' ? null : this.hasPhone() === 'yes',
    kind: this.kind(),
    companySize: this.companySize() || undefined,
  }));

  protected readonly activeFilters = computed(() => {
    const f = this.filter();
    let n = 0;
    if (f.search) n++;
    if (f.document) n++;
    if (f.stage && !this.sameStages(f.stage, OPEN_STAGES)) n++;
    for (const v of [f.city, f.state, f.source, f.importId]) if (v) n++;
    if (f.salaryMin != null || f.salaryMax != null) n++;
    if (f.followUp && f.followUp !== 'Any') n++;
    if (f.hasPhone != null) n++;
    if (f.kind && f.kind !== 'Any') n++;
    if (f.companySize) n++;
    return n;
  });

  // ---- dados ----
  protected readonly options = signal<ProspectFilterOptionsDto | null>(null);
  protected readonly result = signal<PagedResult<ProspectDto> | null>(null);
  protected readonly insights = signal<ProspectInsightsDto | null>(null);
  protected readonly loading = signal(true);

  // ---- selecao / acao em lote ----
  protected readonly selected = signal<Set<string>>(new Set());
  protected readonly bulkStage = signal<ProspectStage>('Contacted');
  protected readonly bulkDate = signal('');
  protected readonly bulkBusy = signal(false);

  protected readonly allOnPageSelected = computed(() => {
    const items = this.result()?.items ?? [];
    return items.length > 0 && items.every(p => this.selected().has(p.id));
  });

  private readonly reload$ = new Subject<void>();

  constructor() {
    this.reload$.pipe(debounceTime(250), takeUntilDestroyed()).subscribe(() => this.load());
    const query = this.route.snapshot.queryParamMap;
    if (query.get('importId')) {
      this.importId.set(query.get('importId')!);
      this.stageFilter.set([...this.stages]);
    }
    this.api.filterOptions().subscribe({ next: o => this.options.set(o), error: () => undefined });
    this.load();
  }

  // ---- mudancas de filtro ----
  protected set<T>(target: { set(v: T): void }, value: T): void {
    target.set(value);
    this.page.set(1);
    this.reload$.next();
  }

  protected toggleStage(stage: ProspectStage): void {
    this.stageFilter.update(list => (list.includes(stage) ? list.filter(s => s !== stage) : [...list, stage]));
    this.page.set(1);
    this.reload$.next();
  }

  protected salary(target: 'min' | 'max', text: string): void {
    const value = text.trim() === '' ? null : Number(text.replace(/\./g, '').replace(',', '.'));
    (target === 'min' ? this.salaryMin : this.salaryMax).set(value === null || Number.isNaN(value) ? null : value);
    this.page.set(1);
    this.reload$.next();
  }

  protected clearFilters(): void {
    this.search.set('');
    this.document.set('');
    this.stageFilter.set([...OPEN_STAGES]);
    this.city.set('');
    this.state.set('');
    this.source.set('');
    this.importId.set('');
    this.salaryMin.set(null);
    this.salaryMax.set(null);
    this.followUp.set('Any');
    this.hasPhone.set('');
    this.kind.set('Any');
    this.companySize.set('');
    this.page.set(1);
    this.load();
  }

  protected goTo(page: number): void {
    this.page.set(page);
    this.loadPage();
  }

  protected load(): void {
    this.loadPage();
    this.api.insights(this.filter()).subscribe({ next: i => this.insights.set(i), error: () => this.insights.set(null) });
  }

  private loadPage(): void {
    this.loading.set(true);
    this.api.search(this.filter(), this.sort(), this.page(), PAGE_SIZE).subscribe({
      next: r => {
        this.result.set(r);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.toast.error(err, 'Não foi possível carregar os prospects.');
      },
    });
  }

  protected changeSort(sort: ProspectSort): void {
    this.sort.set(sort);
    this.page.set(1);
    this.loadPage();
  }

  // ---- selecao ----
  protected toggle(id: string, checked: boolean): void {
    this.selected.update(set => {
      const next = new Set(set);
      if (checked) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  protected togglePage(checked: boolean): void {
    const ids = (this.result()?.items ?? []).map(p => p.id);
    this.selected.update(set => {
      const next = new Set(set);
      for (const id of ids) {
        if (checked) next.add(id);
        else next.delete(id);
      }
      return next;
    });
  }

  protected clearSelection(): void {
    this.selected.set(new Set());
  }

  /** Seleciona todos os que batem com o filtro (nao so a pagina). */
  protected selectAllFiltered(): void {
    this.bulkBusy.set(true);
    this.api.ids(this.filter()).subscribe({
      next: ids => {
        this.bulkBusy.set(false);
        this.selected.set(new Set(ids));
      },
      error: err => {
        this.bulkBusy.set(false);
        this.toast.error(err, 'Não foi possível selecionar todos.');
      },
    });
  }

  protected deleteSelected(): void {
    const ids = [...this.selected()];
    if (!ids.length) return;
    if (!confirm(`Excluir ${ids.length} prospect(s) da prospecção? Esta ação não pode ser desfeita.`)) return;
    this.bulkBusy.set(true);
    this.api.bulkDelete(ids).subscribe({
      next: count => {
        this.bulkBusy.set(false);
        this.toast.success(`${count} prospect(s) excluído(s).`);
        this.clearSelection();
        this.api.filterOptions().subscribe({ next: o => this.options.set(o), error: () => undefined });
        this.load();
      },
      error: err => {
        this.bulkBusy.set(false);
        this.toast.error(err, 'Não foi possível excluir.');
      },
    });
  }

  protected applyBulk(): void {
    const ids = [...this.selected()];
    if (!ids.length) return;
    this.bulkBusy.set(true);
    this.api.bulkStage(ids, this.bulkStage(), this.bulkDate() || null).subscribe({
      next: count => {
        this.bulkBusy.set(false);
        this.toast.success(`${count} prospect(s) → ${this.stageLabels[this.bulkStage()]}${this.bulkDate() ? ', contato agendado' : ''}.`);
        this.clearSelection();
        this.load();
      },
      error: err => {
        this.bulkBusy.set(false);
        this.toast.error(problemMessage(err, 'Não foi possível aplicar a ação em lote.'));
      },
    });
  }

  protected open(p: ProspectDto): void {
    void this.router.navigate(['/prospeccao'], { queryParams: { abrir: p.id } });
  }

  // ---- apresentacao ----
  protected stageLabel(label: string): string {
    return this.stageLabels[label as ProspectStage] ?? label;
  }

  protected bar(item: LabelCount, list: LabelCount[]): number {
    const max = Math.max(1, ...list.map(x => x.count));
    return Math.round((item.count / max) * 100);
  }

  protected followUpState(p: ProspectDto): 'overdue' | 'today' | null {
    if (!p.nextFollowUpOn || p.stage === 'Won' || p.stage === 'Lost') return null;
    return p.nextFollowUpOn < this.today ? 'overdue' : p.nextFollowUpOn === this.today ? 'today' : null;
  }

  protected importLabel(id: string | null): string {
    const item = this.options()?.imports.find(i => i.id === id);
    return item ? item.fileName : '';
  }

  private sameStages(a: ProspectStage[], b: ProspectStage[]): boolean {
    return a.length === b.length && a.every(s => b.includes(s));
  }
}
