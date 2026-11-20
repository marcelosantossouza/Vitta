import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { IMPORT_TEMPLATE_KIND_LABELS, ImportTemplateDto, ImportTemplateKind, ImportTemplatesApi } from '@vitta/core';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { ToastService } from '../../shared/toast.service';

@Component({
  selector: 'vt-template-list',
  imports: [RouterLink, Icon, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <div class="page-header">
        <div>
          <h1>Modelos de planilha</h1>
          <p class="subtitle">Ensine uma vez como ler cada planilha recorrente — a IA sugere, você confere.</p>
        </div>
        <a class="btn btn--primary" routerLink="/modelos/novo"><vt-icon name="sparkles" [size]="18" /> Novo modelo</a>
      </div>

      <div class="tabs" role="tablist" aria-label="Filtrar por tipo">
        <button type="button" role="tab" [class.active]="filter() === ''" (click)="filter.set('')">Todos</button>
        @for (k of kinds; track k) {
          <button type="button" role="tab" [class.active]="filter() === k" (click)="filter.set(k)">{{ kindLabels[k] }}</button>
        }
      </div>

      <div class="card card--flush">
        @if (loading()) {
          <div style="padding: 1rem; display: grid; gap: 0.75rem">@for (i of [1, 2, 3]; track i) { <div class="skeleton" style="height: 2.2rem"></div> }</div>
        } @else if (visible().length === 0) {
          <vt-empty-state icon="sheet" title="Nenhum modelo ainda"
            text="Envie uma planilha de exemplo (da seguradora ou da sua lista de contatos) e a IA monta o modelo para você.">
            <a class="btn btn--primary" routerLink="/modelos/novo" style="margin-top: 0.5rem"><vt-icon name="sparkles" [size]="18" /> Criar modelo com IA</a>
          </vt-empty-state>
        } @else {
          <div class="table-wrap">
            <table class="responsive">
              <thead>
                <tr>
                  <th>Nome</th>
                  <th>Tipo</th>
                  <th>Seguradora</th>
                  <th>Último uso</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (t of visible(); track t.id) {
                  <tr class="clickable" (click)="router.navigate(['/modelos', t.id])">
                    <td data-label="Nome">
                      <span class="name">
                        <a class="primary-cell" [routerLink]="['/modelos', t.id]" (click)="$event.stopPropagation()">{{ t.name }}</a>
                        @if (t.createdByAi) { <span class="badge badge--primary"><vt-icon name="sparkles" [size]="12" /> Criado com IA</span> }
                      </span>
                    </td>
                    <td data-label="Tipo"><span class="badge" [class.badge--info]="t.kind === 'Portfolio'">{{ kindLabels[t.kind] || t.kind }}</span></td>
                    <td data-label="Seguradora">{{ t.insurerName || '—' }}</td>
                    <td data-label="Último uso" class="nowrap">{{ lastUsed(t) }}</td>
                    <td class="actions">
                      <button class="btn btn--ghost btn--icon" type="button" title="Editar" aria-label="Editar modelo"
                        (click)="$event.stopPropagation(); router.navigate(['/modelos', t.id])">
                        <vt-icon name="edit" [size]="16" />
                      </button>
                      <button class="btn btn--ghost btn--icon" type="button" title="Excluir" aria-label="Excluir modelo" (click)="remove(t, $event)">
                        <vt-icon name="trash" [size]="16" />
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>

      <div class="alert">
        <vt-icon name="info" [size]="18" />
        <span>Use os modelos na <a routerLink="/carteira">Carteira</a> (importação mensal da seguradora) e na
          <a routerLink="/prospeccao">Prospecção</a> (importar listas de contatos). Se a seguradora mudar o layout,
          basta editar o modelo ou enviar uma nova planilha de exemplo.</span>
      </div>
    </div>
  `,
  styles: `
    .name { display: inline-flex; flex-wrap: wrap; align-items: center; gap: 0.4rem; justify-content: flex-end; }
    @media (min-width: 721px) { .name { justify-content: flex-start; } }
  `,
})
export class TemplateList {
  private readonly api = inject(ImportTemplatesApi);
  private readonly toast = inject(ToastService);
  protected readonly router = inject(Router);

  protected readonly kinds: ImportTemplateKind[] = ['Portfolio', 'Prospects'];
  protected readonly kindLabels: Record<string, string> = IMPORT_TEMPLATE_KIND_LABELS;
  protected readonly templates = signal<ImportTemplateDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly filter = signal<ImportTemplateKind | ''>('');
  protected readonly visible = computed(() => {
    const kind = this.filter();
    return this.templates().filter(t => !kind || t.kind === kind);
  });

  constructor() {
    this.load();
  }

  private load(): void {
    this.api.list().subscribe({
      next: list => {
        this.templates.set(list);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.toast.error(err, 'Não foi possível carregar os modelos.');
      },
    });
  }

  protected lastUsed(t: ImportTemplateDto): string {
    return t.lastUsedAt ? new Date(t.lastUsedAt).toLocaleDateString('pt-BR') : 'Nunca usado';
  }

  protected remove(t: ImportTemplateDto, event: Event): void {
    event.stopPropagation();
    if (!confirm(`Excluir o modelo "${t.name}"? As importações já feitas não são afetadas.`)) return;
    this.api.delete(t.id).subscribe({
      next: () => {
        this.toast.success('Modelo excluído.');
        this.load();
      },
      error: err => this.toast.error(err, 'Erro ao excluir o modelo.'),
    });
  }
}
