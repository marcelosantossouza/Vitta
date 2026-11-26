import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import {
  AuthService, CONTACT_CHANNEL_LABELS, CONTACT_CHANNELS, ContactChannel, ContactLogDto, ContactsApi, problemMessage, ProspectStage,
} from '@vitta/core';
import { Icon, IconName } from '../../shared/icon';
import { ToastService } from '../../shared/toast.service';

const CHANNEL_ICONS: Record<ContactChannel, IconName> = {
  Call: 'phone', WhatsApp: 'message', Email: 'mail', Visit: 'building', Meeting: 'users', Form: 'link', Other: 'edit',
};

/** "agora" no formato do <input type="datetime-local"> (horario local). */
function localNow(): string {
  const d = new Date();
  d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
  return d.toISOString().slice(0, 16);
}

export interface ContactRegistered {
  nextFollowUpOn: string | null;
  advanced: boolean;
}

/** Historico de contatos (prospect ou cliente): registrar um contato e ver a linha do tempo. */
@Component({
  selector: 'vt-contact-history',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="compose" (submit)="$event.preventDefault(); register()">
      <div class="channels" role="radiogroup" aria-label="Canal">
        @for (c of channels; track c) {
          <button type="button" class="chip" role="radio" [class.on]="channel() === c" [attr.aria-checked]="channel() === c" (click)="channel.set(c)">
            <vt-icon [name]="icons[c]" [size]="14" /> {{ labels[c] }}
          </button>
        }
      </div>
      <textarea rows="3" maxlength="2000" placeholder="O que foi tratado? Ex.: liguei, pediu proposta para ela e o marido…"
        [value]="summary()" (input)="summary.set($any($event.target).value)" aria-label="O que foi tratado"></textarea>
      <div class="row">
        <label class="field">Quando<input type="datetime-local" [value]="when()" (input)="when.set($any($event.target).value); whenEdited = true" /></label>
        @if (prospectId()) {
          <label class="field">Próximo contato<input type="date" [value]="next()" (input)="next.set($any($event.target).value)" /></label>
        }
      </div>
      @if (prospectId() && stage() === 'New') {
        <label class="check"><input type="checkbox" [checked]="advance()" (change)="advance.set($any($event.target).checked)" /> Mover para "Contatado"</label>
      }
      @if (error(); as message) { <div class="alert alert--danger"><vt-icon name="alert" [size]="16" /><span>{{ message }}</span></div> }
      <div class="actions">
        <button class="btn btn--primary btn--sm" type="submit" [disabled]="busy() || !summary().trim()">
          <vt-icon name="check" [size]="14" /> {{ busy() ? 'Salvando…' : 'Registrar contato' }}
        </button>
      </div>
    </form>

    <div class="timeline" [class.timeline--tall]="tall()">
      @if (logs() === null) {
        <div class="skeleton" style="height: 4rem"></div>
      } @else {
        @for (l of logs(); track l.id) {
          <article class="item">
            <span class="item__icon"><vt-icon [name]="icons[l.channel]" [size]="15" /></span>
            <div class="item__body">
              <div class="item__head">
                <strong>{{ labels[l.channel] }}</strong>
                <span class="muted small">{{ format(l.occurredAt) }}@if (l.userName) { · {{ l.userName }} }</span>
                @if (canDelete()) {
                  <button class="btn btn--ghost btn--icon btn--sm del" type="button" (click)="remove(l)" aria-label="Excluir contato"><vt-icon name="x" [size]="12" /></button>
                }
              </div>
              <p>{{ l.summary }}</p>
            </div>
          </article>
        } @empty {
          <p class="muted small empty">Nenhum contato registrado ainda.</p>
        }
      }
    </div>
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 1rem; }
    .compose { display: flex; flex-direction: column; gap: 0.6rem; padding: 0.8rem; border-radius: var(--vt-radius-sm);
      background: var(--vt-surface-2); border: 1px solid var(--vt-border); }
    .channels { display: flex; flex-wrap: wrap; gap: 0.35rem; }
    .chip { display: inline-flex; align-items: center; gap: 0.3rem; border: 1px solid var(--vt-border); background: var(--vt-surface);
      color: var(--vt-muted); border-radius: 999px; padding: 0.3rem 0.65rem; font: inherit; font-size: 0.8rem; font-weight: 600; cursor: pointer;
      &.on { background: var(--vt-primary-soft); border-color: var(--vt-primary); color: var(--vt-primary); } }
    textarea { resize: vertical; }
    .row { display: grid; gap: 0.6rem; grid-template-columns: repeat(auto-fit, minmax(min(100%, 170px), 1fr)); }
    .check { display: flex; align-items: center; gap: 0.5rem; font-size: 0.875rem; cursor: pointer;
      input { width: 16px; height: 16px; min-height: 0; accent-color: var(--vt-primary); } }
    .actions { display: flex; justify-content: flex-end; }
    .timeline { display: flex; flex-direction: column; gap: 0.1rem; max-height: 320px; overflow-y: auto; overscroll-behavior: contain;
      padding-right: 0.25rem; }
    .timeline--tall { max-height: 480px; }
    .item { display: flex; gap: 0.65rem; padding: 0.6rem 0; border-bottom: 1px solid var(--vt-border);
      &:last-child { border-bottom: 0; } }
    .item__icon { display: grid; place-items: center; width: 30px; height: 30px; border-radius: 50%; flex-shrink: 0;
      background: var(--vt-primary-soft); color: var(--vt-primary); }
    .item__body { flex: 1; min-width: 0; p { margin: 0.2rem 0 0; white-space: pre-line; font-size: 0.9rem; overflow-wrap: anywhere; } }
    .item__head { display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap; strong { font-size: 0.875rem; } }
    .del { margin-left: auto; width: 26px; height: 26px; min-height: 0; }
    .empty { margin: 0; padding: 0.5rem 0; }
  `,
})
export class ContactHistory {
  private readonly api = inject(ContactsApi);
  private readonly toast = inject(ToastService);
  protected readonly canDelete = inject(AuthService).canDelete;

  readonly prospectId = input<string | null>(null);
  readonly clientId = input<string | null>(null);
  /** Etapa do prospect (para oferecer "Mover para Contatado"). */
  readonly stage = input<ProspectStage | null>(null);
  readonly tall = input(false);
  /** Avisa quem usa (ex.: atualizar o proximo contato e a etapa no funil). */
  readonly registered = output<ContactRegistered>();
  /** Quantidade de contatos (para o titulo da aba). */
  readonly count = output<number>();

  protected readonly channels = CONTACT_CHANNELS;
  protected readonly labels = CONTACT_CHANNEL_LABELS;
  protected readonly icons = CHANNEL_ICONS;

  protected readonly logs = signal<ContactLogDto[] | null>(null);
  protected readonly channel = signal<ContactChannel>('Call');
  protected readonly summary = signal('');
  protected readonly when = signal(localNow());
  /** So manda a data se a pessoa mudou (senao o servidor usa o horario exato de agora). */
  protected whenEdited = false;
  protected readonly next = signal('');
  protected readonly advance = signal(true);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly target = computed(() => ({ prospectId: this.prospectId(), clientId: this.clientId() }));

  constructor() {
    effect(() => {
      const target = this.target();
      untracked(() => this.load(target));
    });
  }

  private load(target = this.target()): void {
    if (!target.prospectId && !target.clientId) return;
    this.api.list(target).subscribe({
      next: list => {
        this.logs.set(list);
        this.count.emit(list.length);
      },
      error: err => {
        this.logs.set([]);
        this.toast.error(err, 'Não foi possível carregar o histórico.');
      },
    });
  }

  protected register(): void {
    const summary = this.summary().trim();
    if (!summary) return;
    const isProspect = !!this.prospectId();
    const advanced = isProspect && this.stage() === 'New' && this.advance();
    const next = isProspect ? this.next() || null : null;
    this.busy.set(true);
    this.error.set(null);
    this.api.register({
      prospectId: this.prospectId(),
      clientId: isProspect ? null : this.clientId(),
      channel: this.channel(),
      summary,
      occurredAt: this.whenEdited && this.when() ? new Date(this.when()).toISOString() : null,
      nextFollowUpOn: next,
      advanceStage: advanced,
    }).subscribe({
      next: () => {
        this.busy.set(false);
        this.summary.set('');
        this.next.set('');
        this.when.set(localNow());
        this.whenEdited = false;
        this.toast.success('Contato registrado.');
        this.registered.emit({ nextFollowUpOn: next, advanced });
        this.load();
      },
      error: err => {
        this.busy.set(false);
        this.error.set(problemMessage(err, 'Não foi possível registrar o contato.'));
      },
    });
  }

  protected remove(log: ContactLogDto): void {
    if (!confirm('Excluir este registro do histórico?')) return;
    this.api.delete(log.id).subscribe({
      next: () => this.load(),
      error: err => this.toast.error(err, 'Não foi possível excluir.'),
    });
  }

  protected format(iso: string): string {
    return new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: '2-digit', hour: '2-digit', minute: '2-digit' });
  }
}
