import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService, PublicApi, SiteDto } from '@vitta/core';
import { Icon, IconName } from '../../shared/icon';
import { LeadForm } from '../lead-page/lead-form';

/** Icone do produto pelo nome (o resto fica com o escudo). */
function productIcon(name: string): IconName {
  const n = name.toLowerCase();
  if (n.includes('grupo') || n.includes('empresa') || n.includes('funcion')) return 'users';
  if (n.includes('previd') || n.includes('aposent') || n.includes('renda')) return 'money';
  if (n.includes('acidente') || n.includes('doen')) return 'alert';
  return 'shield';
}

/** Home page publica da corretora: capa, sobre, produtos, contato (formulario com consentimento) e rodape com SUSEP. */
@Component({
  selector: 'vt-broker-home',
  imports: [RouterLink, Icon, LeadForm],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './broker-home.html',
  styleUrl: './broker-home.scss',
  host: { '[style.--accent]': 'site()?.accentColor' },
})
export class BrokerHome {
  private readonly api = inject(PublicApi);
  private readonly preview = inject(ActivatedRoute).snapshot.queryParamMap.has('previa') && inject(AuthService).isAuthenticated();

  protected readonly site = signal<SiteDto | null>(null);
  protected readonly state = signal<'loading' | 'off' | 'on'>('loading');
  protected readonly year = new Date().getFullYear();
  protected readonly productIcon = productIcon;

  protected readonly logo = computed(() => this.api.siteImageUrl('logo', this.site()?.logoVersion ?? null));
  protected readonly cover = computed(() => this.api.siteImageUrl('cover', this.site()?.coverVersion ?? null));
  protected readonly initial = computed(() => (this.site()?.brokerName ?? '?').trim().charAt(0).toUpperCase());

  protected readonly whatsAppLink = computed(() => {
    const phone = this.site()?.whatsApp;
    if (!phone) return null;
    const number = phone.length <= 11 ? `55${phone}` : phone;
    return `https://wa.me/${number}?text=${encodeURIComponent('Olá! Vim pelo site e gostaria de falar sobre seguro de vida.')}`;
  });

  protected readonly mapsLink = computed(() => {
    const address = this.site()?.address;
    return address ? `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(address)}` : null;
  });

  protected readonly phoneLink = computed(() => {
    const digits = (this.site()?.phone ?? '').replace(/\D/g, '');
    return digits ? `tel:+55${digits}` : null;
  });

  constructor() {
    this.api.site().subscribe({
      next: s => {
        this.site.set(s);
        this.state.set(s.enabled || this.preview ? 'on' : 'off');
        if (s.brokerName) document.title = `${s.brokerName} — Seguro de vida`;
      },
      error: () => this.state.set('off'),
    });
  }

  protected scrollTo(id: string, event: Event): void {
    event.preventDefault();
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
}
