import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { formatDate, NotificationSettingsDto, problemMessage, SettingsApi } from '@vitta/core';
import { Icon } from '../../shared/icon';
import { ToastService } from '../../shared/toast.service';

const EMAIL = /^[^\s@<>]+@[^\s@<>]+\.[^\s@<>]+$/;

/** Seção "E-mail diário" da tela de Configurações: liga/desliga, destinatários e horário. */
@Component({
  selector: 'vt-notification-settings',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="card block">
      <div class="card__header">
        <h2><vt-icon name="mail" [size]="18" /> E-mail diário</h2>
        @if (data(); as d) {
          @if (d.dailyEmailEnabled) { <span class="badge badge--success">Ligado</span> } @else { <span class="badge">Desligado</span> }
        }
      </div>
      <p class="muted small intro">
        Todo dia, no horário escolhido, a Vitta envia um resumo: contatos de prospecção para hoje (e atrasados), saídas recentes
        da carteira e sua comissão mensal. Cada pessoa recebe em cópia oculta. CPFs nunca vão no e-mail.
      </p>

      @if (loading()) {
        @for (i of [1, 2]; track i) { <div class="skeleton" style="height: 2.6rem"></div> }
      } @else if (data(); as d) {
        @if (!d.emailConfigured) {
          <div class="alert alert--warning">
            <vt-icon name="alert" [size]="18" />
            <span>O envio de e-mail ainda não está configurado no servidor. Você pode salvar a lista agora; os envios começam
              quando o administrador preencher os dados de SMTP no <code>.env</code> (Email__Host, Email__Username, Email__Password, Email__From).</span>
          </div>
        } @else {
          <p class="muted small" style="margin: 0">Enviado por <strong>{{ d.emailFrom }}</strong>
            @if (d.lastSentOn) { · último envio automático em {{ lastSent(d.lastSentOn) }} }</p>
        }

        <label class="switch">
          <input type="checkbox" [checked]="enabled()" (change)="enabled.set($any($event.target).checked)" />
          <span>Enviar o resumo todo dia</span>
        </label>

        <div class="field">
          Destinatários
          <div class="chips" (click)="emailInput.focus()">
            @for (r of recipients(); track r) {
              <span class="chip">{{ r }}
                <button type="button" class="chip__x" (click)="remove(r); $event.stopPropagation()" [attr.aria-label]="'Remover ' + r">
                  <vt-icon name="x" [size]="12" />
                </button>
              </span>
            }
            <input #emailInput type="email" inputmode="email" placeholder="nome@exemplo.com" [value]="draft()"
              (input)="draft.set($any($event.target).value)"
              (keydown.enter)="$event.preventDefault(); add()" (keydown.comma)="$event.preventDefault(); add()"
              (blur)="add()" (paste)="paste($event)" />
          </div>
          <span class="hint">Digite e tecle Enter. Dá para colar vários separados por vírgula ou espaço.</span>
          @if (draftError(); as e) { <span class="field-error">{{ e }}</span> }
        </div>

        <label class="field time">
          Horário (Brasília)
          <input type="time" [value]="sendAt()" (input)="sendAt.set($any($event.target).value)" />
        </label>

        @if (error(); as message) {
          <div class="alert alert--danger"><vt-icon name="alert" [size]="18" /><span>{{ message }}</span></div>
        }

        <div class="form-actions">
          <button class="btn" type="button" (click)="sendNow()" [disabled]="sending() || saving() || !d.emailConfigured"
            title="Envia agora para a lista salva">
            <vt-icon name="mail" [size]="16" /> {{ sending() ? 'Enviando…' : 'Enviar agora' }}
          </button>
          <button class="btn btn--primary" type="button" (click)="save()" [disabled]="saving() || sending()">
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
    .chips {
      display: flex; flex-wrap: wrap; gap: 0.4rem; align-items: center; padding: 0.4rem 0.5rem; min-height: 2.6rem; cursor: text;
      border: 1px solid var(--vt-border); border-radius: var(--vt-radius-sm); background: var(--vt-surface);
      input { flex: 1; min-width: 180px; border: 0; outline: 0; padding: 0.25rem; background: transparent; min-height: 0; box-shadow: none; }
    }
    .chip { display: inline-flex; align-items: center; gap: 0.25rem; padding: 0.2rem 0.3rem 0.2rem 0.6rem; border-radius: 999px;
      background: var(--vt-primary-soft); color: var(--vt-primary); font-size: 0.85rem; font-weight: 550; }
    .chip__x { display: inline-grid; place-items: center; border: 0; background: transparent; color: inherit; cursor: pointer; padding: 2px; border-radius: 50%; }
    .time input { max-width: 160px; }
    code { font-size: 0.85em; }
    .form-actions { display: flex; gap: 0.6rem; justify-content: flex-end; flex-wrap: wrap; }
  `,
})
export class NotificationSettings {
  private readonly api = inject(SettingsApi);
  private readonly toast = inject(ToastService);

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly sending = signal(false);
  protected readonly data = signal<NotificationSettingsDto | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly enabled = signal(false);
  protected readonly recipients = signal<string[]>([]);
  protected readonly sendAt = signal('07:00');
  protected readonly draft = signal('');
  protected readonly draftError = signal<string | null>(null);

  constructor() {
    this.api.getNotifications().subscribe({
      next: d => {
        this.data.set(d);
        this.enabled.set(d.dailyEmailEnabled);
        this.recipients.set(d.recipients);
        this.sendAt.set(d.sendAt);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.toast.error(err, 'Não foi possível carregar o e-mail diário.');
      },
    });
  }

  protected lastSent(iso: string): string {
    return formatDate(iso);
  }

  /** Adiciona o que está digitado (aceita vários separados por vírgula, ponto e vírgula ou espaço). */
  protected add(text = this.draft()): void {
    const parts = text.split(/[\s,;]+/).map(p => p.trim().toLowerCase()).filter(Boolean);
    if (!parts.length) return;
    const invalid = parts.filter(p => !EMAIL.test(p));
    const valid = parts.filter(p => EMAIL.test(p) && !this.recipients().includes(p));
    if (valid.length) this.recipients.update(list => [...list, ...valid]);
    this.draft.set(invalid.join(', '));
    this.draftError.set(invalid.length ? `E-mail inválido: ${invalid.join(', ')}` : null);
  }

  protected paste(event: ClipboardEvent): void {
    const text = event.clipboardData?.getData('text') ?? '';
    if (/[\s,;]/.test(text.trim())) {
      event.preventDefault();
      this.add(text);
    }
  }

  protected remove(email: string): void {
    this.recipients.update(list => list.filter(r => r !== email));
  }

  protected save(): void {
    this.add();
    if (this.draftError()) return;
    if (this.enabled() && this.recipients().length === 0) {
      this.error.set('Adicione ao menos um e-mail para ligar o resumo diário.');
      return;
    }
    this.error.set(null);
    this.saving.set(true);
    const input = { dailyEmailEnabled: this.enabled(), recipients: this.recipients(), sendAt: this.sendAt() || '07:00' };
    this.api.saveNotifications(input).subscribe({
      next: () => {
        this.saving.set(false);
        this.data.update(d => (d ? { ...d, ...input } : d));
        this.toast.success('E-mail diário salvo.');
      },
      error: err => {
        this.saving.set(false);
        this.error.set(problemMessage(err, 'Não foi possível salvar.'));
      },
    });
  }

  protected sendNow(): void {
    this.error.set(null);
    this.sending.set(true);
    this.api.sendDigestNow().subscribe({
      next: count => {
        this.sending.set(false);
        this.toast.success(`Resumo enviado para ${count} destinatário(s).`);
      },
      error: err => {
        this.sending.set(false);
        this.error.set(problemMessage(err, 'Não foi possível enviar. Salve a lista antes de enviar.'));
      },
    });
  }
}
