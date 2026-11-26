import { CdkDrag, CdkDragDrop, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  ImportTemplateDto, ImportTemplatesApi, maskCpf, problemMessage, ProspectDto, ProspectImportResultDto, ProspectsApi,
  ProspectStage, PROSPECT_STAGES, PROSPECT_STAGE_LABELS, todayIso, AuthService } from '@vitta/core';
import { Observable } from 'rxjs';
import { FileOrPaste } from '../../shared/file-or-paste';
import { Icon } from '../../shared/icon';
import { BrlPipe, IsoDatePipe } from '../../shared/pipes';
import { ToastService } from '../../shared/toast.service';
import { ProspectTabs } from './prospect-tabs';

interface Column {
  stage: ProspectStage;
  label: string;
  items: ProspectDto[];
}

@Component({
  selector: 'vt-prospect-board',
  imports: [ReactiveFormsModule, RouterLink, CdkDropListGroup, CdkDropList, CdkDrag, Icon, IsoDatePipe, BrlPipe, FileOrPaste, ProspectTabs],
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
  protected readonly prospects = signal<ProspectDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly search = signal('');
  protected readonly showClosed = signal(true);

  protected readonly dialog = signal<'edit' | 'convert' | 'import' | null>(null);
  protected readonly editing = signal<ProspectDto | null>(null);
  protected readonly busy = signal(false);
  protected readonly dialogError = signal<string | null>(null);

  protected readonly columns = computed<Column[]>(() => {
    const term = this.search().trim().toLowerCase();
    const list = this.prospects().filter(
      p =>
        !term ||
        p.name.toLowerCase().includes(term) ||
        (p.phone ?? '').includes(term) ||
        (p.source ?? '').toLowerCase().includes(term) ||
        (p.city ?? '').toLowerCase().includes(term),
    );
    return this.stages
      .filter(stage => this.showClosed() || (stage !== 'Won' && stage !== 'Lost'))
      .map(stage => ({
        stage,
        label: this.stageLabels[stage],
        items: list
          .filter(p => p.stage === stage)
          .sort((a, b) => (a.nextFollowUpOn ?? '9999').localeCompare(b.nextFollowUpOn ?? '9999')),
      }));
  });

  protected readonly overdueCount = computed(
    () => this.prospects().filter(p => this.isOpen(p) && p.nextFollowUpOn && p.nextFollowUpOn <= this.today).length,
  );

  // ---------- Importacao de planilha ----------
  private readonly templatesApi = inject(ImportTemplatesApi);
  protected readonly importTemplates = signal<ImportTemplateDto[] | null>(null);
  protected readonly importTemplateId = signal('');
  protected readonly importFile = signal<File | null>(null);
  protected readonly importResult = signal<ProspectImportResultDto | null>(null);
  /** Repetidos: completar os campos vazios com os dados da planilha (em vez de so ignorar). */
  protected readonly importUpdateExisting = signal(false);
  protected readonly wizardParams = { kind: 'Prospects', returnUrl: '/prospeccao' };

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
    estimatedSalary: [null as number | null, Validators.min(0)],
    discounts: [null as number | null, Validators.min(0)],
    loans: [null as number | null, Validators.min(0)],
  });

  /** Painel "Perfil e renda" do formulario: aberto quando o prospect ja tem algum desses dados. */
  protected readonly profileOpen = signal(false);

  protected readonly convertForm = this.fb.group({
    cpf: ['', [Validators.required, Validators.pattern(/^\d{3}\.\d{3}\.\d{3}-\d{2}$/)]],
    birthDate: [''],
  });

  constructor() {
    this.load();
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe(params => {
      const id = params.get('abrir');
      if (id) this.pendingOpenId = id;
      this.tryOpenPending();
      // Voltando do assistente de modelos: abre a importacao com o modelo recem-criado.
      const templateId = params.get('templateId');
      if (templateId) this.openImport(templateId);
      else if (params.get('importar')) this.openImport();
    });
  }

  private pendingOpenId: string | null = null;

  private tryOpenPending(): void {
    if (!this.pendingOpenId) return;
    const p = this.prospects().find(x => x.id === this.pendingOpenId);
    if (p) {
      this.pendingOpenId = null;
      this.openEdit(p);
    }
  }

  protected load(): void {
    this.api.list().subscribe({
      next: list => {
        this.prospects.set(list);
        this.loading.set(false);
        this.tryOpenPending();
      },
      error: err => {
        this.loading.set(false);
        this.toast.error(err, 'Não foi possível carregar a prospecção.');
      },
    });
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
    // Atualizacao otimista
    this.prospects.update(list => list.map(p => (p.id === prospect.id ? { ...p, stage } : p)));
    this.api.changeStage(prospect.id, { stage, nextFollowUpOn: prospect.nextFollowUpOn }).subscribe({
      next: () => this.toast.success(`${prospect.name} → ${this.stageLabels[stage]}`),
      error: err => {
        this.prospects.update(list => list.map(p => (p.id === prospect.id ? { ...p, stage: previous } : p)));
        this.toast.error(err, 'Não foi possível mover o prospect.');
      },
    });
  }

  private readonly emptyProfile = {
    phone2: '', occupation: '', address: '', city: '', state: '', zipCode: '', birthDate: '',
    estimatedSalary: null, discounts: null, loans: null,
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
      estimatedSalary: p.estimatedSalary,
      discounts: p.discounts,
      loans: p.loans,
    });
    this.profileOpen.set(
      [p.phone2, p.occupation, p.address, p.city, p.state, p.zipCode, p.birthDate, p.estimatedSalary, p.discounts, p.loans].some(v => v != null && v !== ''),
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
        estimatedSalary: this.amount(v.estimatedSalary),
        discounts: this.amount(v.discounts),
        loans: this.amount(v.loans),
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
    this.convertForm.reset({ cpf: '', birthDate: '' });
    this.dialogError.set(null);
    this.dialog.set('convert');
  }

  protected onCpfInput(event: Event): void {
    const el = event.target as HTMLInputElement;
    el.value = maskCpf(el.value);
    this.convertForm.controls.cpf.setValue(el.value);
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
