import { CdkDrag, CdkDragDrop, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, ElementRef, inject, signal, viewChild, WritableSignal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  ImportTemplateDto, ImportTemplatesApi, maskCpf, problemMessage, ProspectDto, ProspectImportResultDto, ProspectsApi,
  ProspectStage, PROSPECT_STAGES, PROSPECT_STAGE_LABELS, todayIso, AuthService, formatCnpj, formatCpf, FOLLOW_UP_FILTER_LABELS,
  FollowUpFilter, LeadKind, ProspectFilter, ProspectFilterOptionsDto } from '@vitta/core';
import { Observable } from 'rxjs';
import { FileOrPaste } from '../../shared/file-or-paste';
import { Icon } from '../../shared/icon';
import { mediaQuery, MOBILE_QUERY } from '../../shared/media';
import { Sheet } from '../../shared/sheet';
import { BrlPipe, IsoDatePipe } from '../../shared/pipes';
import { ToastService } from '../../shared/toast.service';
import { ProspectTabs } from './prospect-tabs';

interface Column {
  stage: ProspectStage;
  label: string;
  /** Quantos existem na etapa com os filtros atuais (os cards mostrados sao so os primeiros). */
  total: number;
  items: ProspectDto[];
  loadingMore: boolean;
}

/** Cards por coluna a cada carga ("Mostrar mais" traz outro bloco). */
const PAGE = 30;

@Component({
  selector: 'vt-prospect-board',
  imports: [ReactiveFormsModule, RouterLink, NgTemplateOutlet, CdkDropListGroup, CdkDropList, CdkDrag, Icon, IsoDatePipe, BrlPipe, FileOrPaste, ProspectTabs, Sheet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './prospect-board.html',
  styleUrl: './prospect-board.scss',
})
export class ProspectBoard {
  /** Assistentes nao excluem (a API tambem bloqueia). */
  protected readonly canDelete = inject(AuthService).canDelete;
  private readonly api = inject(ProspectsApi);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly today = todayIso();
  protected readonly stages = PROSPECT_STAGES;
  protected readonly stageLabels = PROSPECT_STAGE_LABELS;
  protected readonly followUpLabels = FOLLOW_UP_FILTER_LABELS;
  protected readonly followUpOptions = Object.keys(FOLLOW_UP_FILTER_LABELS) as FollowUpFilter[];
  protected readonly board = signal<Column[]>([]);
  protected readonly loading = signal(true);
  protected readonly showClosed = signal(true);
  protected readonly options = signal<ProspectFilterOptionsDto | null>(null);
  protected readonly overdueCount = signal(0);

  // ---------- Filtros (aplicados no servidor: com milhares de leads, so os primeiros cards de cada etapa vem) ----------
  protected readonly search = signal('');
  protected readonly document = signal('');
  protected readonly kind = signal<LeadKind>('Any');
  protected readonly city = signal('');
  protected readonly state = signal('');
  protected readonly importId = signal('');
  protected readonly followUp = signal<FollowUpFilter>('Any');
  protected readonly filtersOpen = signal(false);

  protected readonly filter = computed<ProspectFilter>(() => ({
    search: this.search().trim() || undefined,
    document: this.document().replace(/\D/g, '') || undefined,
    kind: this.kind(),
    city: this.city() || undefined,
    state: this.state() || undefined,
    importId: this.importId() || undefined,
    followUp: this.followUp(),
  }));

  protected readonly activeFilters = computed(
    () => [this.search().trim(), this.document().replace(/\D/g, ''), this.city(), this.state(), this.importId()].filter(Boolean).length
      + (this.kind() !== 'Any' ? 1 : 0) + (this.followUp() !== 'Any' ? 1 : 0),
  );

  protected readonly columns = computed(() =>
    this.board().filter(c => this.showClosed() || (c.stage !== 'Won' && c.stage !== 'Lost')),
  );

  protected readonly totalShown = computed(() => this.columns().reduce((n, c) => n + c.total, 0));

  // ---------- Celular: uma etapa por tela (deslizar), abas das etapas e "Mover" em painel ----------
  protected readonly isMobile = mediaQuery(MOBILE_QUERY);
  protected readonly activeIndex = signal(0);
  protected readonly moveSheetFor = signal<ProspectDto | null>(null);
  private readonly boardEl = viewChild<ElementRef<HTMLElement>>('boardEl');
  private readonly tabsEl = viewChild<ElementRef<HTMLElement>>('tabsEl');

  /** Ao deslizar o quadro, marca a aba da etapa que esta na tela. */
  protected onBoardScroll(): void {
    const board = this.boardEl()?.nativeElement;
    if (!board || !this.isMobile()) return;
    const cols = Array.from(board.children) as HTMLElement[];
    let best = 0;
    cols.forEach((c, i) => {
      if (Math.abs(c.offsetLeft - board.scrollLeft) < Math.abs(cols[best]!.offsetLeft - board.scrollLeft)) best = i;
    });
    if (best !== this.activeIndex()) {
      this.activeIndex.set(best);
      (this.tabsEl()?.nativeElement.children[best] as HTMLElement | undefined)?.scrollIntoView({ inline: 'nearest', block: 'nearest' });
    }
  }

  protected goToStage(index: number): void {
    const board = this.boardEl()?.nativeElement;
    const col = board?.children[index] as HTMLElement | undefined;
    if (!board || !col) return;
    this.activeIndex.set(index);
    board.scrollTo({ left: col.offsetLeft, behavior: 'smooth' });
  }

  protected nextStage(p: ProspectDto): ProspectStage | null {
    const i = this.stages.indexOf(p.stage);
    return p.stage === 'Won' || p.stage === 'Lost' || i < 0 || i >= this.stages.length - 2 ? null : this.stages[i + 1]!;
  }

  protected moveFromSheet(p: ProspectDto, stage: ProspectStage): void {
    this.moveSheetFor.set(null);
    this.moveTo(p, stage);
  }

  protected telLink(p: ProspectDto): string | null {
    const d = (p.phone ?? p.phone2 ?? '').replace(/\D/g, '');
    return d.length >= 10 ? `tel:+55${d.slice(-11)}` : null;
  }

  protected waLink(p: ProspectDto): string | null {
    const d = (p.phone ?? p.phone2 ?? '').replace(/\D/g, '');
    return d.length >= 10 ? `https://wa.me/55${d.slice(-11)}` : null;
  }

  // ---------- Importacao de planilha ----------
  private readonly templatesApi = inject(ImportTemplatesApi);
  protected readonly importTemplates = signal<ImportTemplateDto[] | null>(null);
  protected readonly importTemplateId = signal('');
  protected readonly importFile = signal<File | null>(null);
  protected readonly importResult = signal<ProspectImportResultDto | null>(null);
  /** Repetidos: completar os campos vazios com os dados da planilha (em vez de so ignorar). */
  protected readonly importUpdateExisting = signal(false);
  protected readonly wizardParams = { kind: 'Prospects', returnUrl: '/prospeccao' };

  protected readonly dialog = signal<'edit' | 'convert' | 'import' | null>(null);
  protected readonly editing = signal<ProspectDto | null>(null);
  protected readonly busy = signal(false);
  protected readonly dialogError = signal<string | null>(null);

  protected readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    phone: [''],
    email: ['', Validators.email],
    source: [''],
    nextFollowUpOn: [''],
    notes: [''],
    // Perfil (listas de prospeccao)
    phone2: [''],
    occupation: [''],
    address: [''],
    city: [''],
    state: ['', Validators.maxLength(30)],
    zipCode: [''],
    birthDate: [''],
    cpf: [''],
    estimatedSalary: [null as number | null, Validators.min(0)],
    discounts: [null as number | null, Validators.min(0)],
    loans: [null as number | null, Validators.min(0)],
    cnpj: [''],
    legalName: [''],
    activity: [''],
    companySize: [''],
    openedOn: [''],
  });

  /** Painel "Perfil e renda" do formulario: aberto quando o prospect ja tem algum desses dados. */
  protected readonly profileOpen = signal(false);

  protected readonly convertForm = this.fb.group({
    cpf: ['', [Validators.required, Validators.pattern(/^\d{3}\.\d{3}\.\d{3}-\d{2}$/)]],
    birthDate: [''],
  });

  constructor() {
    this.load();
    this.api.filterOptions().subscribe({ next: o => this.options.set(o), error: () => this.options.set(null) });
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe(params => {
      const id = params.get('abrir');
      if (id) this.openById(id);
      // Voltando do assistente de modelos: abre a importacao com o modelo recem-criado.
      const templateId = params.get('templateId');
      if (templateId) this.openImport(templateId);
      else if (params.get('importar')) this.openImport();
    });
  }

  /** Link direto (?abrir=id): usa o card carregado ou busca na API (pode estar alem dos primeiros da coluna). */
  private openById(id: string): void {
    const loaded = this.board().flatMap(c => c.items).find(p => p.id === id);
    if (loaded) {
      this.openEdit(loaded);
      return;
    }
    this.api.get(id).subscribe({
      next: p => this.openEdit(p),
      error: err => this.toast.error(err, 'Prospect não encontrado.'),
    });
  }

  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  /** Campos de texto: espera a pessoa parar de digitar antes de buscar. */
  protected typed(target: 'search' | 'document', value: string): void {
    (target === 'search' ? this.search : this.document).set(value);
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.load(), 350);
  }

  /** Selects: aplica na hora. */
  protected setFilter<T>(target: WritableSignal<T>, value: T): void {
    target.set(value);
    this.load();
  }

  protected clearFilters(): void {
    this.search.set('');
    this.document.set('');
    this.kind.set('Any');
    this.city.set('');
    this.state.set('');
    this.importId.set('');
    this.followUp.set('Any');
    this.load();
  }

  private loadSeq = 0;

  protected load(): void {
    const seq = ++this.loadSeq;
    const filter = this.filter();
    this.api.board(filter, PAGE).subscribe({
      next: cols => {
        if (seq !== this.loadSeq) return; // resposta antiga (filtro ja mudou)
        this.board.set(cols.map(c => ({ ...c, label: this.stageLabels[c.stage], loadingMore: false })));
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.toast.error(err, 'Não foi possível carregar a prospecção.');
      },
    });
    this.api.insights(filter).subscribe({ next: i => seq === this.loadSeq && this.overdueCount.set(i.overdue + i.dueToday), error: () => {} });
  }

  /** Proximo bloco da coluna (mesma ordem do quadro: proximo contato). */
  protected loadMore(col: Column): void {
    const page = Math.floor(col.items.length / PAGE) + 1;
    this.patchColumn(col.stage, c => ({ ...c, loadingMore: true }));
    this.api.search({ ...this.filter(), stage: [col.stage] }, 'FollowUp', page, PAGE).subscribe({
      next: result => this.patchColumn(col.stage, c => {
        const seen = new Set(c.items.map(p => p.id));
        return { ...c, total: result.totalCount, loadingMore: false, items: [...c.items, ...result.items.filter(p => !seen.has(p.id))] };
      }),
      error: err => {
        this.patchColumn(col.stage, c => ({ ...c, loadingMore: false }));
        this.toast.error(err, 'Não foi possível carregar mais.');
      },
    });
  }

  private patchColumn(stage: ProspectStage, fn: (c: Column) => Column): void {
    this.board.update(cols => cols.map(c => (c.stage === stage ? fn(c) : c)));
  }

  protected isOpen(p: ProspectDto): boolean {
    return p.stage !== 'Won' && p.stage !== 'Lost';
  }

  protected followUpState(p: ProspectDto): 'overdue' | 'today' | 'future' | null {
    if (!p.nextFollowUpOn || !this.isOpen(p)) return null;
    if (p.nextFollowUpOn < this.today) return 'overdue';
    if (p.nextFollowUpOn === this.today) return 'today';
    return 'future';
  }

  protected drop(event: CdkDragDrop<Column>): void {
    if (event.previousContainer === event.container) return;
    const prospect = event.item.data as ProspectDto;
    const stage = event.container.data.stage;
    this.moveTo(prospect, stage);
  }

  protected moveTo(prospect: ProspectDto, stage: ProspectStage): void {
    if (prospect.stage === stage) return;
    const previous = prospect.stage;
    // Atualizacao otimista: tira da coluna de origem e poe no topo da de destino, ajustando os totais.
    this.shift(prospect, previous, stage);
    this.api.changeStage(prospect.id, { stage, nextFollowUpOn: prospect.nextFollowUpOn }).subscribe({
      next: () => this.toast.success(`${prospect.name} → ${this.stageLabels[stage]}`),
      error: err => {
        this.shift({ ...prospect, stage }, stage, previous);
        this.toast.error(err, 'Não foi possível mover o prospect.');
      },
    });
  }

  private shift(prospect: ProspectDto, from: ProspectStage, to: ProspectStage): void {
    const moved = { ...prospect, stage: to };
    this.board.update(cols => cols.map(c => {
      if (c.stage === from) return { ...c, total: Math.max(0, c.total - 1), items: c.items.filter(p => p.id !== prospect.id) };
      if (c.stage === to) return { ...c, total: c.total + 1, items: [moved, ...c.items] };
      return c;
    }));
  }

  private readonly emptyProfile = {
    phone2: '', occupation: '', address: '', city: '', state: '', zipCode: '', birthDate: '', cpf: '',
    estimatedSalary: null, discounts: null, loans: null,
    cnpj: '', legalName: '', activity: '', companySize: '', openedOn: '',
  };

  protected openNew(): void {
    this.editing.set(null);
    this.profileOpen.set(false);
    this.form.reset({ name: '', phone: '', email: '', source: '', nextFollowUpOn: '', notes: '', ...this.emptyProfile });
    this.dialogError.set(null);
    this.dialog.set('edit');
  }

  protected openEdit(p: ProspectDto): void {
    this.editing.set(p);
    this.form.reset({
      name: p.name,
      phone: p.phone ?? '',
      email: p.email ?? '',
      source: p.source ?? '',
      nextFollowUpOn: p.nextFollowUpOn ?? '',
      notes: p.notes ?? '',
      phone2: p.phone2 ?? '',
      occupation: p.occupation ?? '',
      address: p.address ?? '',
      city: p.city ?? '',
      state: p.state ?? '',
      zipCode: p.zipCode ?? '',
      birthDate: p.birthDate ?? '',
      cpf: formatCpf(p.cpf),
      estimatedSalary: p.estimatedSalary,
      discounts: p.discounts,
      loans: p.loans,
      cnpj: formatCnpj(p.cnpj),
      legalName: p.legalName ?? '',
      activity: p.activity ?? '',
      companySize: p.companySize ?? '',
      openedOn: p.openedOn ?? '',
    });
    this.profileOpen.set(
      [p.phone2, p.occupation, p.address, p.city, p.state, p.zipCode, p.birthDate, p.cpf, p.estimatedSalary, p.discounts, p.loans, p.cnpj]
        .some(v => v != null && v !== ''),
    );
    this.dialogError.set(null);
    this.dialog.set('edit');
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const body = {
      name: v.name.trim(),
      phone: v.phone.trim() || null,
      email: v.email.trim() || null,
      source: v.source.trim() || null,
      notes: v.notes.trim() || null,
      nextFollowUpOn: v.nextFollowUpOn || null,
      profile: {
        phone2: v.phone2.trim() || null,
        occupation: v.occupation.trim() || null,
        address: v.address.trim() || null,
        city: v.city.trim() || null,
        state: v.state.trim() || null,
        zipCode: v.zipCode.trim() || null,
        birthDate: v.birthDate || null,
        cpf: v.cpf.trim() || null,
        estimatedSalary: this.amount(v.estimatedSalary),
        discounts: this.amount(v.discounts),
        loans: this.amount(v.loans),
        cnpj: v.cnpj.trim() || null,
        legalName: v.legalName.trim() || null,
        activity: v.activity.trim() || null,
        companySize: v.companySize || null,
        openedOn: v.openedOn || null,
      },
    };
    const current = this.editing();
    this.busy.set(true);
    const request$: Observable<unknown> = current ? this.api.update(current.id, body) : this.api.create(body);
    request$.subscribe({
      next: () => {
        this.toast.success(current ? 'Prospect atualizado.' : 'Prospect adicionado.');
        this.closeDialog();
        this.load();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.dialogError.set(problemMessage(err, 'Erro ao salvar prospect.'));
      },
    });
  }

  private amount(value: number | string | null): number | null {
    if (value === null || value === '') return null;
    const n = Number(value);
    return Number.isNaN(n) ? null : n;
  }

  protected remove(): void {
    const p = this.editing();
    if (!p || !confirm(`Excluir ${p.name} da prospecção?`)) return;
    this.api.delete(p.id).subscribe({
      next: () => {
        this.toast.success('Prospect excluído.');
        this.closeDialog();
        this.load();
      },
      error: err => this.toast.error(err, 'Erro ao excluir prospect.'),
    });
  }

  protected openConvert(): void {
    const p = this.editing();
    this.convertForm.reset({ cpf: maskCpf(p?.cpf ?? ''), birthDate: p?.birthDate ?? '' });
    this.dialogError.set(null);
    this.dialog.set('convert');
  }

  protected onCpfInput(event: Event): void {
    const el = event.target as HTMLInputElement;
    el.value = maskCpf(el.value);
    this.convertForm.controls.cpf.setValue(el.value);
  }

  protected onProfileCpf(event: Event): void {
    const el = event.target as HTMLInputElement;
    el.value = maskCpf(el.value);
    this.form.controls.cpf.setValue(el.value);
  }

  protected convert(): void {
    const p = this.editing();
    if (!p || this.convertForm.invalid) {
      this.convertForm.markAllAsTouched();
      return;
    }
    const v = this.convertForm.getRawValue();
    this.busy.set(true);
    this.api.convert(p.id, { cpf: v.cpf, birthDate: v.birthDate || null }).subscribe({
      next: ({ clientId }) => {
        this.toast.success(`${p.name} agora é cliente!`);
        this.closeDialog();
        void this.router.navigate(['/clientes', clientId]);
      },
      error: err => {
        this.busy.set(false);
        this.dialogError.set(problemMessage(err, 'Erro ao converter em cliente.'));
      },
    });
  }

  protected openImport(preselectId?: string): void {
    this.importFile.set(null);
    this.importResult.set(null);
    this.importUpdateExisting.set(false);
    this.importTemplates.set(null);
    this.dialogError.set(null);
    this.busy.set(false);
    this.dialog.set('import');
    this.templatesApi.list('Prospects').subscribe({
      next: list => {
        this.importTemplates.set(list);
        const recent = [...list].sort((a, b) => (b.lastUsedAt ?? '').localeCompare(a.lastUsedAt ?? ''))[0];
        this.importTemplateId.set(list.find(t => t.id === preselectId)?.id ?? recent?.id ?? '');
      },
      error: err => {
        this.importTemplates.set([]);
        this.dialogError.set(problemMessage(err, 'Não foi possível carregar os modelos.'));
      },
    });
  }

  /** Arquivo escolhido ou texto colado do site (ja validados pelo `vt-file-or-paste`). */
  protected onImportFile(file: File | null): void {
    this.dialogError.set(null);
    this.importFile.set(file);
  }

  protected runImport(): void {
    const file = this.importFile();
    const templateId = this.importTemplateId();
    if (!file || !templateId) {
      this.dialogError.set('Escolha o modelo e a planilha (ou cole o texto do site).');
      return;
    }
    this.busy.set(true);
    this.dialogError.set(null);
    this.api.importFile(file, templateId, this.importUpdateExisting()).subscribe({
      next: result => {
        this.busy.set(false);
        this.importResult.set(result);
        this.toast.success(`${result.created} prospect(s) importado(s)${result.updated ? `, ${result.updated} completado(s)` : ''}.`);
        this.load();
      },
      error: err => {
        this.busy.set(false);
        this.dialogError.set(problemMessage(err, 'Não foi possível importar a planilha.'));
      },
    });
  }

  protected closeDialog(): void {
    this.dialog.set(null);
    this.busy.set(false);
    const query = this.route.snapshot.queryParamMap;
    if (query.has('abrir') || query.has('templateId') || query.has('importar')) {
      void this.router.navigate([], { queryParams: {}, replaceUrl: true });
    }
  }
}
