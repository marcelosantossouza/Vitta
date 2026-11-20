import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  ColumnMapping, IMPORT_TEMPLATE_KIND_LABELS, ImportFieldDto, ImportTemplateDto, ImportTemplateInput, ImportTemplateKind,
  ImportTemplatesApi, InsurerDto, InsurersApi, problemMessage, SpreadsheetAnalysisDto,
} from '@vitta/core';
import { map, Observable } from 'rxjs';
import { FileOrPaste, FileOrPasteMode, isPastedFileName } from '../../shared/file-or-paste';
import { Icon } from '../../shared/icon';
import { ToastService } from '../../shared/toast.service';

const PREVIEW_ROWS = 8;
const CONFIDENCE_LABELS = { high: 'Confiança alta', medium: 'Confiança média', low: 'Confiança baixa' } as const;

/** Normaliza o texto do cabecalho para comparar colunas (sem espacos extras, sem diferenciar maiusculas). */
function norm(value: string | null | undefined): string {
  return (value ?? '').trim().replace(/\s+/g, ' ').toLowerCase();
}

function isKind(value: string | null): value is ImportTemplateKind {
  return value === 'Portfolio' || value === 'Prospects';
}

/** Aceita apenas caminhos internos (evita redirecionar para outro site). */
function safeReturnUrl(value: string | null): string | null {
  return value && value.startsWith('/') && !value.startsWith('//') ? value : null;
}

/** Nomes das colunas na linha de cabecalho. Linha 0 = sem cabecalho: "Coluna 1", "Coluna 2"... pela posicao. */
function headersOf(rows: (string | null)[][], headerRow: number): string[] {
  if (headerRow === 0) {
    const width = Math.max(0, ...rows.map(r => r.length));
    return Array.from({ length: width }, (_, i) => `Coluna ${i + 1}`);
  }
  return (rows[headerRow - 1] ?? []).map(h => (h ?? '').trim());
}

function toMapping(columns: ColumnMapping[]): Record<string, string[]> {
  const map: Record<string, string[]> = {};
  for (const c of columns) {
    if (!c.column?.trim()) continue;
    (map[c.field] ??= []).push(c.column.trim());
  }
  return map;
}

@Component({
  selector: 'vt-template-wizard',
  imports: [NgTemplateOutlet, Icon, FileOrPaste],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './template-wizard.html',
  styleUrl: './template-wizard.scss',
})
export class TemplateWizard {
  private readonly api = inject(ImportTemplatesApi);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly kindLabels = IMPORT_TEMPLATE_KIND_LABELS;
  protected readonly kinds: ImportTemplateKind[] = ['Portfolio', 'Prospects'];
  protected readonly confidenceLabels = CONFIDENCE_LABELS;

  private readonly params = this.route.snapshot.queryParamMap;
  protected readonly templateId = this.route.snapshot.paramMap.get('id');
  protected readonly isEdit = !!this.templateId;
  protected readonly returnUrl = safeReturnUrl(this.params.get('returnUrl'));
  /** Tipo veio da tela de origem (carteira/prospeccao) e nao precisa ser escolhido. */
  protected readonly kindFixed = isKind(this.params.get('kind'));

  protected readonly template = signal<ImportTemplateDto | null>(null);
  protected readonly loadingTemplate = signal(this.isEdit);
  protected readonly insurers = signal<InsurerDto[]>([]);
  protected readonly fields = signal<ImportFieldDto[]>([]);

  protected readonly kind = signal<ImportTemplateKind | null>(null);
  protected readonly insurerId = signal('');
  protected readonly name = signal('');

  protected readonly file = signal<File | null>(null);
  /** Modo de envio (arquivo ou texto colado), mantido ao trocar de planilha. */
  protected readonly inputMode = signal<FileOrPasteMode>('file');
  protected readonly isPasted = isPastedFileName;
  protected readonly analyzing = signal(false);
  protected readonly analysis = signal<SpreadsheetAnalysisDto | null>(null);
  protected readonly headerRow = signal(1);
  /** Aba informada manualmente (modo edicao sem planilha). */
  protected readonly sheetText = signal('');
  /** Mapeamento "cru" campo -> colunas. Com planilha, so vale o que existe no cabecalho atual. */
  protected readonly mapping = signal<Record<string, string[]>>({});

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly triedSave = signal(false);

  /** Etapa 2 = conferir mapeamento (com analise, ou editando um modelo existente). */
  protected readonly step = computed(() => (this.analysis() || this.isEdit ? 2 : 1));

  protected readonly headers = computed<string[]>(() => {
    const a = this.analysis();
    if (!a) return [];
    return headersOf(a.rows, this.headerRow());
  });

  /** Opcoes de coluna (cabecalhos nao vazios, sem repeticao). */
  protected readonly headerOptions = computed(() => {
    const seen = new Set<string>();
    return this.headers().filter(h => {
      const key = norm(h);
      if (!key || seen.has(key)) return false;
      seen.add(key);
      return true;
    });
  });

  protected readonly rowOptions = computed(() => [
    { value: 0, label: 'Sem cabeçalho — colunas pela posição' },
    ...(this.analysis()?.rows ?? []).map((row, i) => {
      const sample = row.map(c => (c ?? '').trim()).filter(Boolean).slice(0, 3).join(' · ');
      return { value: i + 1, label: `Linha ${i + 1}${sample ? ' — ' + (sample.length > 48 ? sample.slice(0, 47) + '…' : sample) : ' (vazia)'}` };
    }),
  ]);

  protected readonly previewRows = computed(() => {
    const a = this.analysis();
    if (!a) return [];
    const start = this.headerRow();
    return a.rows.slice(start, start + PREVIEW_ROWS).map((cells, i) => ({ number: start + i + 1, cells }));
  });

  protected readonly columnCount = computed(() =>
    Math.max(this.headers().length, ...this.previewRows().map(r => r.cells.length), 0),
  );
  protected readonly columnIndexes = computed(() => Array.from({ length: this.columnCount() }, (_, i) => i));

  /** Mapeamento efetivo: com planilha, descarta colunas que nao existem na linha de cabecalho escolhida. */
  protected readonly effective = computed<Record<string, string[]>>(() => {
    const raw = this.mapping();
    if (!this.analysis()) return raw;
    const byKey = new Map(this.headerOptions().map(h => [norm(h), h]));
    const result: Record<string, string[]> = {};
    for (const [field, cols] of Object.entries(raw)) {
      const found = cols.map(c => byKey.get(norm(c))).filter((c): c is string => !!c);
      if (found.length) result[field] = [...new Set(found)];
    }
    return result;
  });

  /** Para cada coluna da pre-visualizacao, os campos do Vitta que ela alimenta. */
  protected readonly columnFields = computed(() => {
    const labels = new Map(this.fields().map(f => [f.field, f.label]));
    const byColumn = new Map<string, string[]>();
    for (const [field, cols] of Object.entries(this.effective())) {
      for (const c of cols) {
        const key = norm(c);
        byColumn.set(key, [...(byColumn.get(key) ?? []), labels.get(field) ?? field]);
      }
    }
    return this.headers().map(h => byColumn.get(norm(h)) ?? []);
  });

  protected readonly missingRequired = computed(() =>
    this.fields().filter(f => f.required && !(this.effective()[f.field]?.length)),
  );

  constructor() {
    inject(InsurersApi).list().subscribe({ next: list => this.insurers.set(list) });

    if (this.isEdit) {
      this.api.get(this.templateId!).subscribe({
        next: t => {
          this.template.set(t);
          this.name.set(t.name);
          this.insurerId.set(t.insurerId ?? '');
          this.headerRow.set(t.headerRow ?? 1);
          this.sheetText.set(t.sheetName ?? '');
          this.mapping.set(toMapping(t.columns));
          this.setKind(t.kind);
          this.loadingTemplate.set(false);
        },
        error: err => {
          this.loadingTemplate.set(false);
          this.toast.error(err, 'Não foi possível carregar o modelo.');
          void this.router.navigate(['/modelos']);
        },
      });
    } else {
      const kind = this.params.get('kind');
      this.insurerId.set(this.params.get('insurerId') ?? '');
      if (isKind(kind)) this.setKind(kind);
    }
  }

  protected setKind(kind: ImportTemplateKind): void {
    if (this.kind() === kind) return;
    this.kind.set(kind);
    this.fields.set([]);
    this.analysis.set(null);
    this.api.fields(kind).subscribe({
      next: list => this.fields.set(list),
      error: err => this.toast.error(err, 'Não foi possível carregar os campos do modelo.'),
    });
    if (kind !== 'Portfolio') this.insurerId.set('');
    if (this.file() && !this.isEdit) this.analyze();
  }

  // ---------- Arquivo / analise ----------

  /** Arquivo escolhido ou texto colado (ja validados pelo `vt-file-or-paste`). */
  protected setFile(file: File | null): void {
    this.error.set(null);
    if (!file) return;
    this.file.set(file);
    if (!this.kind()) {
      this.error.set('Escolha o tipo de planilha para a IA saber o que procurar.');
      return;
    }
    this.analyze();
  }

  /** Envia a planilha para a IA. Sem `sheetName`, a API escolhe a aba. */
  protected analyze(sheetName?: string | null, firstPass = true): void {
    const file = this.file();
    const kind = this.kind();
    if (!file || !kind) return;
    this.analyzing.set(true);
    this.error.set(null);
    this.api.analyze(file, kind, sheetName).subscribe({
      next: result => {
        // Editando: se a planilha tem a aba do modelo, analisa essa aba.
        const wanted = this.template()?.sheetName;
        if (firstPass && !sheetName && wanted && result.sheetName !== wanted && result.sheetName !== null
          && result.sheets.some(s => norm(s) === norm(wanted))) {
          this.analyze(result.sheets.find(s => norm(s) === norm(wanted)), false);
          return;
        }
        this.applyAnalysis(result);
        this.analyzing.set(false);
      },
      error: err => {
        this.analyzing.set(false);
        this.error.set(problemMessage(err, 'Não foi possível ler a planilha. Confira o arquivo e tente de novo.'));
      },
    });
  }

  private applyAnalysis(result: SpreadsheetAnalysisDto): void {
    const suggestion = toMapping(result.columns);
    const template = this.template();
    let headerRow = result.headerRow ?? 1;
    let mapping = suggestion;

    if (template) {
      // Mantem o mapeamento do modelo onde as colunas ainda existem; completa com a sugestao da IA.
      // Linha 0 (sem cabecalho): as colunas sao "Coluna 1", "Coluna 2"... pela posicao.
      const headersAt = (row: number) => new Set(headersOf(result.rows, row).map(norm).filter(Boolean));
      const existing = toMapping(template.columns);
      const countAt = (row: number) => Object.values(existing).flat().filter(c => headersAt(row).has(norm(c))).length;
      const templateCount = template.headerRow != null ? countAt(template.headerRow) : 0;
      if (templateCount > 0 && templateCount >= countAt(headerRow)) headerRow = template.headerRow;
      const headers = headersAt(headerRow);
      mapping = { ...suggestion };
      for (const [field, cols] of Object.entries(existing)) {
        const kept = cols.filter(c => headers.has(norm(c)));
        if (kept.length) mapping[field] = kept;
      }
    } else if (!this.name().trim()) {
      this.name.set(result.suggestedName ?? '');
    }

    this.analysis.set(result);
    this.headerRow.set(Math.min(Math.max(0, headerRow), Math.max(1, result.rows.length)));
    this.mapping.set(mapping);
  }

  protected changeSheet(sheet: string): void {
    this.analyze(sheet, false);
  }

  protected restart(): void {
    this.analysis.set(null);
    this.file.set(null);
    this.error.set(null);
    if (!this.isEdit) this.mapping.set({});
  }

  // ---------- Mapeamento ----------

  protected selected(field: string): string[] {
    return this.effective()[field] ?? [];
  }

  protected setSingle(field: string, column: string): void {
    this.mapping.update(m => ({ ...m, [field]: column ? [column] : [] }));
  }

  protected toggleMulti(field: string, column: string, checked: boolean): void {
    const current = this.selected(field);
    const next = checked ? [...current, column] : current.filter(c => norm(c) !== norm(column));
    this.mapping.update(m => ({ ...m, [field]: next }));
  }

  /** Modo texto (editando sem planilha): varias colunas, uma por linha. */
  protected setText(field: string, value: string, multiple: boolean): void {
    const cols = (multiple ? value.split(/\r?\n/) : [value]).map(c => c.trim()).filter(Boolean);
    this.mapping.update(m => ({ ...m, [field]: cols }));
  }

  /** Linha do cabecalho digitada no modo edicao sem planilha (0 = sem cabecalho). */
  protected setHeaderRowText(value: string): void {
    const n = Math.floor(Number(value));
    this.headerRow.set(value.trim() === '' || !Number.isFinite(n) ? 1 : Math.max(0, n));
  }

  protected textValue(field: string): string {
    return (this.mapping()[field] ?? []).join('\n');
  }

  protected isMissing(field: ImportFieldDto): boolean {
    return this.triedSave() && this.missingRequired().some(f => f.field === field.field);
  }

  // ---------- Salvar ----------

  protected save(): void {
    this.triedSave.set(true);
    const kind = this.kind();
    const name = this.name().trim();
    if (!kind) {
      this.error.set('Escolha o tipo de planilha.');
      return;
    }
    if (!name) {
      this.error.set('Dê um nome ao modelo (ex.: "Carteira MAG").');
      return;
    }
    const missing = this.missingRequired();
    if (missing.length) {
      this.error.set(`Escolha a coluna de: ${missing.map(f => f.label).join(', ')}.`);
      return;
    }

    const analysis = this.analysis();
    const template = this.template();
    const columns: ColumnMapping[] = Object.entries(this.effective()).flatMap(([field, cols]) => cols.map(column => ({ field, column })));
    const body: ImportTemplateInput = {
      name,
      kind,
      insurerId: kind === 'Portfolio' ? this.insurerId() || null : null,
      sheetName: analysis ? analysis.sheetName : this.sheetText().trim() || null,
      headerRow: this.headerRow(),
      columns,
      createdByAi: analysis ? analysis.analyzedBy === 'ai' || !!template?.createdByAi : !!template?.createdByAi,
      aiNotes: analysis?.notes ?? template?.aiNotes ?? null,
    };

    this.saving.set(true);
    this.error.set(null);
    const request$: Observable<string> = template
      ? this.api.update(template.id, body).pipe(map(() => template.id))
      : this.api.create(body).pipe(map(r => r.id));

    request$.subscribe({
      next: id => {
        this.toast.success(template ? 'Modelo atualizado.' : 'Modelo criado. Já pode usar na importação!');
        this.leave(id);
      },
      error: err => {
        this.saving.set(false);
        this.error.set(problemMessage(err, 'Não foi possível salvar o modelo.'));
      },
    });
  }

  private leave(id: string | null): void {
    if (this.returnUrl) {
      const tree = this.router.parseUrl(this.returnUrl);
      if (id) tree.queryParams = { ...tree.queryParams, templateId: id };
      void this.router.navigateByUrl(tree);
    } else {
      void this.router.navigate(['/modelos']);
    }
  }

  protected cancel(): void {
    this.leave(null);
  }
}
