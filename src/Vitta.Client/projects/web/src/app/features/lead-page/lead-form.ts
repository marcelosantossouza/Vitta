import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { problemMessage, PublicApi } from '@vitta/core';
import { Icon } from '../../shared/icon';

const UFS = ['AC', 'AL', 'AM', 'AP', 'BA', 'CE', 'DF', 'ES', 'GO', 'MA', 'MG', 'MS', 'MT', 'PA', 'PB', 'PE', 'PI', 'PR', 'RJ', 'RN', 'RO', 'RR',
  'RS', 'SC', 'SE', 'SP', 'TO'];

/** "21987654321" -> "(21) 98765-4321" enquanto digita. */
function maskPhone(value: string): string {
  const d = value.replace(/\D/g, '').slice(0, 11);
  if (d.length <= 2) return d.length ? `(${d}` : '';
  if (d.length <= 6) return `(${d.slice(0, 2)}) ${d.slice(2)}`;
  if (d.length <= 10) return `(${d.slice(0, 2)}) ${d.slice(2, 6)}-${d.slice(6)}`;
  return `(${d.slice(0, 2)}) ${d.slice(2, 7)}-${d.slice(7)}`;
}

/** Formulario de pedido de contato com consentimento (usado na /simular e na home da corretora). */
@Component({
  selector: 'vt-lead-form',
  imports: [ReactiveFormsModule, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (sent()) {
      <div class="sent">
        <span class="ok"><vt-icon name="check" [size]="32" /></span>
        <h2>Recebemos seu pedido!</h2>
        <p class="muted">{{ brokerName() }} vai entrar em contato em breve para entender o que você precisa.</p>
        @if (whatsAppLink(); as link) {
          <a class="btn btn--primary btn--lg" [href]="link" target="_blank" rel="noopener"><vt-icon name="phone" [size]="18" /> Falar agora pelo WhatsApp</a>
        }
      </div>
    } @else {
      <form class="lead-form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <div class="form-grid">
          <label class="field span-all">Nome *
            <input formControlName="name" autocomplete="name" />
            @if (form.controls.name.touched && form.controls.name.invalid) { <span class="field-error">Informe seu nome.</span> }
          </label>
          <label class="field">Telefone / WhatsApp *
            <input type="tel" formControlName="phone" autocomplete="tel" inputmode="numeric" placeholder="(21) 99999-9999" maxlength="15" (input)="onPhone($event)" />
            @if (form.controls.phone.touched && form.controls.phone.invalid) { <span class="field-error">Informe o telefone com DDD.</span> }
          </label>
          <label class="field">E-mail
            <input type="email" formControlName="email" autocomplete="email" />
            @if (form.controls.email.touched && form.controls.email.invalid) { <span class="field-error">E-mail inválido.</span> }
          </label>
          <label class="field">Cidade<input formControlName="city" autocomplete="address-level2" /></label>
          <label class="field">UF
            <select formControlName="state">
              <option value="">—</option>
              @for (u of ufs; track u) { <option [value]="u">{{ u }}</option> }
            </select>
          </label>
          <label class="field">Data de nascimento<input type="date" formControlName="birthDate" autocomplete="bday" /></label>
          <label class="field">O que você procura?
            <select formControlName="interest">
              <option value="">—</option>
              <option>Proteger a família</option>
              <option>Garantir a renda se eu ficar sem trabalhar</option>
              <option>Seguro para a minha empresa / funcionários</option>
              <option>Planejar a aposentadoria</option>
              <option>Só quero entender as opções</option>
            </select>
          </label>
          <!-- Campo invisivel anti-robo -->
          <label class="hp" aria-hidden="true">Site<input formControlName="website" tabindex="-1" autocomplete="off" /></label>
        </div>

        <label class="consent">
          <input type="checkbox" formControlName="consent" />
          <span>{{ consentText() }}</span>
        </label>
        @if (form.controls.consent.touched && form.controls.consent.invalid) { <span class="field-error">Marque a autorização para podermos falar com você.</span> }

        @if (error(); as message) {
          <div class="alert alert--danger"><vt-icon name="alert" [size]="18" /><span>{{ message }}</span></div>
        }
        <button class="btn btn--primary btn--lg" type="submit" [disabled]="busy()">{{ busy() ? 'Enviando…' : 'Quero ser contatado' }}</button>
        <p class="muted small fine">Seus dados são usados só para este contato. Para pedir a exclusão, é só responder qualquer mensagem nossa.</p>
      </form>
    }
  `,
  styles: `
    :host { display: block; }
    .lead-form { display: flex; flex-direction: column; gap: 1rem; }
    .sent { display: flex; flex-direction: column; align-items: center; gap: 0.75rem; text-align: center; padding: 1rem 0;
      h2 { margin: 0; } p { margin: 0; } }
    .ok { display: grid; place-items: center; width: 64px; height: 64px; border-radius: 50%; background: var(--vt-primary-soft); color: var(--vt-primary); }
    .consent {
      display: flex; gap: 0.6rem; align-items: flex-start; font-size: 0.85rem; line-height: 1.45; cursor: pointer;
      padding: 0.75rem; border-radius: var(--vt-radius-sm); background: var(--vt-surface-2);
      input { width: 18px; height: 18px; min-height: 0; margin-top: 0.1rem; flex-shrink: 0; accent-color: var(--vt-primary); }
    }
    /* Pote de mel: fora da tela, sem display:none (alguns robos ignoram campos escondidos assim) */
    .hp { position: absolute; left: -9999px; width: 1px; height: 1px; overflow: hidden; }
    .fine { text-align: center; margin: 0; }
  `,
})
export class LeadForm {
  private readonly api = inject(PublicApi);

  readonly brokerName = input.required<string>();
  readonly consentText = input.required<string>();
  readonly whatsApp = input<string | null>(null);
  /** De onde veio (?origem=, ou "site" na home). */
  readonly origin = input<string | null>(null);

  protected readonly ufs = UFS;
  protected readonly sent = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly whatsAppLink = computed(() => {
    const phone = this.whatsApp();
    if (!phone) return null;
    const number = phone.length <= 11 ? `55${phone}` : phone;
    const text = encodeURIComponent(`Olá! Acabei de pedir contato sobre seguro de vida. Meu nome é ${this.form.controls.name.value}.`);
    return `https://wa.me/${number}?text=${text}`;
  });

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    phone: ['', [Validators.required, Validators.pattern(/^\(\d{2}\) \d{4,5}-\d{4}$/)]],
    email: ['', Validators.email],
    city: [''],
    state: [''],
    birthDate: [''],
    interest: [''],
    consent: [false, Validators.requiredTrue],
    website: [''], // pote de mel: invisivel para pessoas
  });

  protected onPhone(event: Event): void {
    const el = event.target as HTMLInputElement;
    el.value = maskPhone(el.value);
    this.form.controls.phone.setValue(el.value);
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.api.submitLead({
      name: v.name.trim(),
      phone: v.phone,
      email: v.email.trim() || null,
      city: v.city.trim() || null,
      state: v.state || null,
      birthDate: v.birthDate || null,
      interest: v.interest || null,
      origin: this.origin()?.slice(0, 60) ?? null,
      consent: v.consent,
      website: v.website || null,
    }).subscribe({
      next: () => {
        this.busy.set(false);
        this.sent.set(true);
      },
      error: err => {
        this.busy.set(false);
        this.error.set((err as { status?: number })?.status === 429
          ? 'Muitos envios seguidos. Espere um minuto e tente de novo.'
          : problemMessage(err, 'Não foi possível enviar. Tente de novo em instantes.'));
      },
    });
  }
}
