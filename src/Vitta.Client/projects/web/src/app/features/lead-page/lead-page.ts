import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { LeadPageDto, problemMessage, PublicApi } from '@vitta/core';
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

/**
 * Pagina publica de captacao (/simular): a pessoa pede contato e autoriza ser procurada (LGPD: consentimento).
 * O link pode levar ?origem=instagram (ou whatsapp, convenio-x...) para saber de onde veio cada lead.
 */
@Component({
  selector: 'vt-lead-page',
  imports: [ReactiveFormsModule, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './lead-page.html',
  styleUrl: './lead-page.scss',
})
export class LeadPage {
  private readonly api = inject(PublicApi);
  private readonly origin = inject(ActivatedRoute).snapshot.queryParamMap.get('origem')?.slice(0, 60) ?? null;

  protected readonly ufs = UFS;
  protected readonly page = signal<LeadPageDto | null>(null);
  protected readonly state = signal<'loading' | 'off' | 'form' | 'sent'>('loading');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly whatsAppLink = computed(() => {
    const p = this.page();
    if (!p?.whatsApp) return null;
    const number = p.whatsApp.length <= 11 ? `55${p.whatsApp}` : p.whatsApp;
    const text = encodeURIComponent(`Olá! Acabei de pedir contato pela página sobre seguro de vida. Meu nome é ${this.form.controls.name.value}.`);
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

  constructor() {
    this.api.leadPage().subscribe({
      next: p => {
        this.page.set(p);
        this.state.set(p.enabled ? 'form' : 'off');
        document.title = `${p.brokerName} — Seguro de vida`;
      },
      error: () => this.state.set('off'),
    });
  }

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
      origin: this.origin,
      consent: v.consent,
      website: v.website || null,
    }).subscribe({
      next: () => {
        this.busy.set(false);
        this.state.set('sent');
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
