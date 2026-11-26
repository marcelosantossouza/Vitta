import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService, ProspectImportItemDto, ProspectsApi } from '@vitta/core';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { IsoDatePipe } from '../../shared/pipes';
import { ToastService } from '../../shared/toast.service';
import { ProspectTabs } from './prospect-tabs';

/** Historico de importacoes de prospeccao, com a opcao de desfazer (excluir o lote). */
@Component({
  selector: 'vt-prospect-imports',
  imports: [RouterLink, Icon, IsoDatePipe, EmptyState, ProspectTabs],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <div class="page-header">
        <div>
          <h1>Prospecção</h1>
          <p class="subtitle">Cada planilha importada vira um lote. Excluir o lote desfaz a importação.</p>
        </div>
        <a class="btn" routerLink="/prospeccao" [queryParams]="{ importar: 1 }"><vt-icon name="upload" [size]="18" /> Importar planilha</a>
      </div>

      <vt-prospect-tabs />

      <div class="card card--flush">
        @if (loading()) {
          <div style="padding: 1rem; display: grid; gap: 0.75rem">@for (n of [1, 2]; track n) { <div class="skeleton" style="height: 2.2rem"></div> }</div>
        } @else if (imports().length === 0) {
          <vt-empty-state icon="upload" title="Nenhuma importação ainda" text="Importe uma planilha de prospects pela aba Funil." />
        } @else {
          <div class="table-wrap">
            <table class="responsive">
              <thead>
                <tr><th>Arquivo</th><th>Modelo</th><th>Data</th><th class="num">Linhas</th><th class="num">Criados</th>
                  <th class="num">Completados</th><th class="num">Ignorados</th><th class="num">Ainda em "Novo"</th><th></th></tr>
              </thead>
              <tbody>
                @for (i of imports(); track i.id) {
                  <tr>
                    <td data-label="Arquivo"><strong>{{ i.fileName }}</strong></td>
                    <td data-label="Modelo">{{ i.templateName }}</td>
                    <td data-label="Data" class="nowrap">{{ i.createdAt | isoDate }}</td>
                    <td data-label="Linhas" class="num">{{ i.totalRows }}</td>
                    <td data-label="Criados" class="num">{{ i.created }}</td>
                    <td data-label="Completados" class="num">{{ i.updated }}</td>
                    <td data-label="Ignorados" class="num">{{ i.skipped }}</td>
                    <td data-label='Ainda em "Novo"' class="num">{{ i.stillNew }} de {{ i.stillLinked }}</td>
                    <td class="actions">
                      <a class="btn btn--sm btn--ghost" routerLink="/prospeccao/lista" [queryParams]="{ importId: i.id }">
                        <vt-icon name="search" [size]="14" /> Ver na lista</a>
                      @if (canDelete()) {
                        <button class="btn btn--sm btn--ghost" type="button" (click)="remove(i)" [disabled]="busy() === i.id">
                          <vt-icon name="trash" [size]="14" /> Excluir</button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>
  `,
  styles: `.actions { display: flex; gap: 0.35rem; justify-content: flex-end; flex-wrap: wrap; }`,
})
export class ProspectImports {
  private readonly api = inject(ProspectsApi);
  private readonly toast = inject(ToastService);
  protected readonly canDelete = inject(AuthService).canDelete;

  protected readonly imports = signal<ProspectImportItemDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly busy = signal<string | null>(null);

  constructor() {
    this.load();
  }

  private load(): void {
    this.api.filterOptions().subscribe({
      next: o => {
        this.imports.set(o.imports);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.toast.error(err, 'Não foi possível carregar as importações.');
      },
    });
  }

  protected remove(i: ProspectImportItemDto): void {
    const worked = i.stillLinked - i.stillNew;
    const message =
      `Excluir a importação "${i.fileName}"?\n\n` +
      `• ${i.stillNew} prospect(s) ainda em "Novo" serão removidos.\n` +
      (worked > 0 ? `• ${worked} que já avançaram no funil (ou viraram cliente) ficam.\n` : '') +
      (i.updated > 0 ? `• Os dados completados em ${i.updated} prospect(s) que já existiam não são desfeitos.` : '');
    if (!confirm(message)) return;
    this.busy.set(i.id);
    this.api.deleteImport(i.id).subscribe({
      next: r => {
        this.busy.set(null);
        this.toast.success(`Importação excluída: ${r.removed} removido(s)${r.kept ? `, ${r.kept} mantido(s)` : ''}.`);
        this.load();
      },
      error: err => {
        this.busy.set(null);
        this.toast.error(err, 'Não foi possível excluir a importação.');
      },
    });
  }
}
