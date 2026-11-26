import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { LeadPageDto, PublicApi } from '@vitta/core';
import { Icon } from '../../shared/icon';
import { LeadForm } from './lead-form';

/**
 * Pagina publica de captacao (/simular): a pessoa pede contato e autoriza ser procurada (LGPD: consentimento).
 * O link pode levar ?origem=instagram (ou whatsapp, convenio-x...) para saber de onde veio cada lead.
 */
@Component({
  selector: 'vt-lead-page',
  imports: [Icon, LeadForm],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="lead">
      <div class="lead__card">
        @switch (state()) {
          @case ('loading') { <div class="skeleton" style="height: 18rem"></div> }
          @case ('off') {
            <div class="center">
              <h1>Página indisponível</h1>
              <p class="muted">Esta página de contato não está ativa no momento.</p>
            </div>
          }
          @case ('form') {
            @if (page(); as p) {
              <p class="broker"><vt-icon name="shield" [size]="18" /> {{ p.brokerName }}</p>
              <h1>{{ p.headline }}</h1>
              <p class="muted">Deixe seu contato e receba, sem compromisso, uma proposta pensada para a sua família e o seu bolso.</p>
              <vt-lead-form [brokerName]="p.brokerName" [consentText]="p.consentText" [whatsApp]="p.whatsApp" [origin]="origin" />
            }
          }
        }
      </div>
    </main>
  `,
  styleUrl: './lead-page.scss',
})
export class LeadPage {
  private readonly api = inject(PublicApi);
  protected readonly origin = inject(ActivatedRoute).snapshot.queryParamMap.get('origem')?.slice(0, 60) ?? null;

  protected readonly page = signal<LeadPageDto | null>(null);
  protected readonly state = signal<'loading' | 'off' | 'form'>('loading');

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
}
