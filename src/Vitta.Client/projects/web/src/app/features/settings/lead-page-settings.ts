import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { problemMessage, SettingsApi } from '@vitta/core';
import { Icon } from '../../shared/icon';
import { ToastService } from '../../shared/toast.service';

/** Seção "Página de captação": a página pública /simular onde a pessoa pede contato e autoriza ser procurada. */
@Component({
  selector: 'vt-lead-page-settings',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="card block">
      <div class="card__header">
        <h2><vt-icon name="link" [size]="18" /> Página de captação</h2>
        @if (!loading()) {
          @if (savedEnabled()) { <span class="badge badge--success">No ar</span> } @else { <span class="badge">Desligada</span> }
        }
      </div>
      <p class="muted small intro">
        Uma página pública para divulgar no Instagram, WhatsApp, cartão de visita ou em convênios. Quem preenche entra no funil
        de Prospecção (etapa Novo, contato agendado para o próximo dia útil) com a autorização registrada — é o jeito certo,
        pela LGPD, de conseguir contatos de pessoas.
      </p>

      @if (loading()) {
        @for (i of [1, 2]; track i) { <div class="skeleton" style="height: 2.6rem"></div> }
      } @else {
        <label class="switch">
          <input type="checkbox" [checked]="enabled()" (change)="enabled.set($any($event.target).checked)" />
          <span>Deixar a página no ar</span>
        </label>
        <div class="form-grid">
          <label class="field">Nome que aparece na página *
            <input [value]="brokerName()" (input)="brokerName.set($any($event.target).value)" placeholder="Ex.: João Silva Corretor de Seguros" maxlength="120" />
          </label>
          <label class="field">WhatsApp (botão depois do envio)
            <input type="tel" inputmode="numeric" [value]="whatsApp()" (input)="whatsApp.set($any($event.target).value)" placeholder="(21) 99999-9999" />
          </label>
          <label class="field span-all">Título
            <input [value]="headline()" (input)="headline.set($any($event.target).value)" maxlength="140" />
          </label>
        </div>

        <div class="field">
          Link da página
          <div class="link">
            <code>{{ link() }}</code>
            <button class="btn btn--sm" type="button" (click)="copy(link())"><vt-icon name="copy" [size]="14" /> Copiar</button>
            <a class="btn btn--sm btn--ghost" [href]="link()" target="_blank" rel="noopener">Abrir</a>
          </div>
          <span class="hint">Para saber de onde veio cada contato, use um link por canal (aparece na Origem do prospect):</span>
          <ul class="origins">
            @for (o of origins; track o) {
              <li><code>{{ link() }}?origem={{ o }}</code>
                <button class="btn btn--ghost btn--icon btn--sm" type="button" (click)="copy(link() + '?origem=' + o)" [attr.aria-label]="'Copiar link ' + o"><vt-icon name="copy" [size]="14" /></button></li>
            }
          </ul>
        </div>

        @if (error(); as message) {
          <div class="alert alert--danger"><vt-icon name="alert" [size]="18" /><span>{{ message }}</span></div>
        }
        <div class="form-actions">
          <button class="btn btn--primary" type="button" (click)="save()" [disabled]="saving()">
            <vt-icon name="check" [size]="16" /> {{ saving() ? 'Salvando…' : 'Salvar' }}
          </button>
        </div>
      }
    </section>
  `,
  styles: `
    .block { display: flex; flex-direction: column; gap: 1rem; max-width: 720px; }
    .block h2 { display: flex; align-items: center; gap: 0.5rem; margin: 0; }
    .intro { margin: -0.25rem 0 0; }
    .switch { display: flex; align-items: center; gap: 0.6rem; font-weight: 550; cursor: pointer;
      input { width: 18px; height: 18px; accent-color: var(--vt-primary); } }
    .link { display: flex; flex-wrap: wrap; align-items: center; gap: 0.5rem; }
    code { font-size: 0.85em; word-break: break-all; background: var(--vt-surface-2); padding: 0.2rem 0.4rem; border-radius: 6px; }
    .origins { margin: 0; padding: 0; list-style: none; display: flex; flex-direction: column; gap: 0.25rem;
      li { display: flex; align-items: center; gap: 0.35rem; } }
    .form-actions { display: flex; gap: 0.6rem; justify-content: flex-end; }
  `,
})
export class LeadPageSettings {
  private readonly api = inject(SettingsApi);
  private readonly toast = inject(ToastService);

  protected readonly origins = ['instagram', 'whatsapp', 'facebook', 'indicacao'];
  protected readonly link = computed(() => `${location.origin}/simular`);

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly savedEnabled = signal(false);
  protected readonly enabled = signal(false);
  protected readonly brokerName = signal('');
  protected readonly headline = signal('');
  protected readonly whatsApp = signal('');

  constructor() {
    this.api.getLeadPage().subscribe({
      next: s => {
        this.savedEnabled.set(s.enabled);
        this.enabled.set(s.enabled);
        this.brokerName.set(s.brokerName);
        this.headline.set(s.headline);
        this.whatsApp.set(s.whatsApp ?? '');
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.error.set(problemMessage(err, 'Não foi possível carregar a página de captação.'));
      },
    });
  }

  protected save(): void {
    this.saving.set(true);
    this.error.set(null);
    const input = {
      enabled: this.enabled(),
      brokerName: this.brokerName().trim(),
      headline: this.headline().trim(),
      whatsApp: this.whatsApp().trim() || null,
    };
    this.api.saveLeadPage(input).subscribe({
      next: () => {
        this.saving.set(false);
        this.savedEnabled.set(input.enabled);
        this.toast.success(input.enabled ? 'Página de captação no ar.' : 'Página de captação salva (desligada).');
      },
      error: err => {
        this.saving.set(false);
        this.error.set(problemMessage(err, 'Não foi possível salvar.'));
      },
    });
  }

  protected copy(text: string): void {
    navigator.clipboard?.writeText(text).then(
      () => this.toast.success('Link copiado.'),
      () => this.toast.error(null, 'Não foi possível copiar.'),
    );
  }
}
