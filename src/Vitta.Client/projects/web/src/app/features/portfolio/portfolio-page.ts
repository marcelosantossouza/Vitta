import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  ImportTemplateDto, ImportTemplatesApi, InsurerDto, InsurersApi, PortfolioApi, PortfolioImportDto, problemMessage,
} from '@vitta/core';
import { catchError, forkJoin, of } from 'rxjs';
import { EmptyState } from '../../shared/empty-state';
import { FileOrPaste } from '../../shared/file-or-paste';
import { Icon } from '../../shared/icon';
import { MonthPipe } from '../../shared/pipes';
import { ToastService } from '../../shared/toast.service';

/** Modelos da seguradora primeiro, do uso mais recente para o mais antigo. */
function byRecentUse(a: ImportTemplateDto, b: ImportTemplateDto): number {
  return (b.lastUsedAt ?? '').localeCompare(a.lastUsedAt ?? '') || a.name.localeCompare(b.name);
}

function previousMonth(): string {
  const d = new Date();
  d.setDate(1);
  d.setMonth(d.getMonth() - 1);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
}

@Component({
  selector: 'vt-portfolio-page',
  imports: [RouterLink, Icon, EmptyState, MonthPipe, FileOrPaste],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './portfolio-page.html',
  styleUrl: './portfolio-page.scss',
})
export class PortfolioPage {
  private readonly api = inject(PortfolioApi);
  private readonly toast = inject(ToastService);
  protected readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly insurers = signal<InsurerDto[]>([]);
  protected readonly imports = signal<PortfolioImportDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly uploading = signal(false);
  protected readonly error = signal<string | null>(null);
  /** Modelo usado na tentativa que falhou (para oferecer o atalho "ajustar o modelo"). */
  protected readonly lastErrorWithTemplate = signal<string | null>(null);

  protected readonly insurerId = signal('');
  protected readonly month = signal(previousMonth());
  protected readonly file = signal<File | null>(null);

  protected readonly templates = signal<ImportTemplateDto[]>([]);
  protected readonly templateId = signal('');
  /** Modelos que servem para a seguradora escolhida (dela ou sem seguradora). */
  protected readonly availableTemplates = computed(() =>
    this.templates()
      .filter(t => !t.insurerId || t.insurerId === this.insurerId())
      .sort((a, b) => Number(!!b.insurerId) - Number(!!a.insurerId) || byRecentUse(a, b)),
  );

  constructor() {
    forkJoin({
      insurers: inject(InsurersApi).list(),
      templates: inject(ImportTemplatesApi).list('Portfolio').pipe(catchError(() => of([] as ImportTemplateDto[]))),
    }).subscribe({
      next: ({ insurers, templates }) => {
        this.insurers.set(insurers);
        this.templates.set(templates);
        // Voltando do assistente de modelos: ja seleciona o modelo criado.
        const fromWizard = templates.find(t => t.id === this.route.snapshot.queryParamMap.get('templateId'));
        const insurerParam = this.route.snapshot.queryParamMap.get('insurerId');
        const mag = insurers.find(i => i.code === 'MAG');
        const insurer = fromWizard?.insurerId ?? insurers.find(i => i.id === insurerParam)?.id ?? (mag ?? insurers[0])?.id ?? '';
        this.insurerId.set(insurer);
        if (fromWizard) {
          this.templateId.set(fromWizard.id);
          void this.router.navigate([], { queryParams: {}, replaceUrl: true });
        } else {
          this.selectDefaultTemplate();
        }
      },
      error: err => this.toast.error(err, 'Não foi possível carregar as seguradoras.'),
    });
    this.load();
  }

  protected selectInsurer(id: string): void {
    this.insurerId.set(id);
    this.selectDefaultTemplate();
  }

  protected selectTemplate(id: string): void {
    this.templateId.set(id);
    const template = this.templates().find(t => t.id === id);
    if (template?.insurerId) this.insurerId.set(template.insurerId);
  }

  /** Pre-seleciona o modelo mais recente da seguradora (ou deteccao automatica se ela nao tiver). */
  private selectDefaultTemplate(): void {
    const own = this.templates().filter(t => t.insurerId === this.insurerId()).sort(byRecentUse);
    this.templateId.set(own[0]?.id ?? '');
  }

  protected readonly wizardParams = computed(() => ({
    kind: 'Portfolio',
    insurerId: this.insurerId() || null,
    returnUrl: '/carteira',
  }));

  private load(): void {
    this.api.list().subscribe({
      next: list => {
        this.imports.set(list);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.toast.error(err, 'Não foi possível carregar as importações.');
      },
    });
  }

  /** Arquivo escolhido ou texto colado do site (ja validados pelo `vt-file-or-paste`). */
  protected setFile(file: File | null): void {
    this.error.set(null);
    this.file.set(file);
  }

  protected upload(): void {
    const file = this.file();
    if (!file || !this.insurerId() || !this.month()) {
      this.error.set('Escolha a seguradora, o mês de referência e o arquivo (ou cole o texto do site).');
      return;
    }
    this.uploading.set(true);
    this.error.set(null);
    this.lastErrorWithTemplate.set(null);
    const templateId = this.templateId() || null;
    this.api.upload({ insurerId: this.insurerId(), referenceMonth: this.month(), file, templateId }).subscribe({
      next: result => {
        this.toast.success(
          result.isFirstImport
            ? `Carteira base registrada (${result.totalRows} apólices).`
            : `Importação concluída: ${result.entered} entradas, ${result.left} saídas, ${result.changed} mudanças.`,
        );
        void this.router.navigate(['/carteira', result.id], { state: { bookSync: result.bookSync ?? null } });
      },
      error: err => {
        this.uploading.set(false);
        this.error.set(problemMessage(err, 'Não foi possível importar a planilha.'));
        this.lastErrorWithTemplate.set(templateId);
      },
    });
  }

  protected remove(item: PortfolioImportDto, event: Event): void {
    event.stopPropagation();
    if (!confirm(`Excluir a importação de ${item.insurerName}?`)) return;
    this.api.delete(item.id).subscribe({
      next: () => {
        this.toast.success('Importação excluída.');
        this.load();
      },
      error: err => this.toast.error(err, 'Erro ao excluir importação.'),
    });
  }
}
