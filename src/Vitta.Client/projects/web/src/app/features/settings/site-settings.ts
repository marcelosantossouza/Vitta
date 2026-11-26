import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { problemMessage, PublicApi, SettingsApi, SiteDto, SiteImageKind } from '@vitta/core';
import { Icon } from '../../shared/icon';
import { ToastService } from '../../shared/toast.service';

/** Reduz a imagem no navegador antes de enviar (capa ate 1600px, logo ate 400px). */
function resize(file: File, maxSide: number): Promise<Blob> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => {
      const scale = Math.min(1, maxSide / Math.max(img.width, img.height));
      const canvas = document.createElement('canvas');
      canvas.width = Math.round(img.width * scale);
      canvas.height = Math.round(img.height * scale);
      canvas.getContext('2d')!.drawImage(img, 0, 0, canvas.width, canvas.height);
      URL.revokeObjectURL(img.src);
      // Logo em PNG (mantem fundo transparente); capa em JPEG (bem menor).
      canvas.toBlob(b => (b ? resolve(b) : reject(new Error('blob'))), maxSide <= 400 ? 'image/png' : 'image/jpeg', 0.85);
    };
    img.onerror = () => reject(new Error('Imagem inválida.'));
    img.src = URL.createObjectURL(file);
  });
}

/** Seção "Site da corretora": a home page pública (logo, capa, sobre, contato, redes, SUSEP). */
@Component({
  selector: 'vt-site-settings',
  imports: [ReactiveFormsModule, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="card block">
      <div class="card__header">
        <h2><vt-icon name="building" [size]="18" /> Site da corretora</h2>
        @if (site(); as s) { @if (s.enabled) { <span class="badge badge--success">No ar</span> } @else { <span class="badge">Desligado</span> } }
      </div>
      <p class="muted small intro">
        Uma home page simples para a corretora, com o formulário de contato da página de captação. O nome, o título e o WhatsApp
        são os mesmos da seção <strong>Página de captação</strong>.
      </p>

      @if (!site()) {
        @for (i of [1, 2, 3]; track i) { <div class="skeleton" style="height: 2.6rem"></div> }
      } @else {
        <form [formGroup]="form" (ngSubmit)="save()" novalidate class="block">
          <label class="switch"><input type="checkbox" formControlName="enabled" /><span>Deixar o site no ar</span></label>

          <div class="images">
            @for (k of kinds; track k.kind) {
              <div class="image">
                <span class="image__label">{{ k.label }}</span>
                <div class="image__preview" [class.image__preview--cover]="k.kind === 'cover'">
                  @if (imageUrl(k.kind); as src) { <img [src]="src" alt="" /> } @else { <span class="muted small">Sem imagem</span> }
                </div>
                <div class="image__actions">
                  <label class="btn btn--sm">
                    <vt-icon name="upload" [size]="14" /> {{ imageUrl(k.kind) ? 'Trocar' : 'Enviar' }}
                    <input type="file" accept="image/png,image/jpeg,image/webp" hidden (change)="upload(k.kind, $event)" />
                  </label>
                  @if (imageUrl(k.kind)) { <button class="btn btn--ghost btn--sm" type="button" (click)="removeImage(k.kind)">Remover</button> }
                </div>
                <span class="hint">{{ k.hint }}</span>
              </div>
            }
          </div>

          <div class="form-grid">
            <label class="field span-all">Sobre a corretora
              <textarea formControlName="about" rows="4" maxlength="2000" placeholder="Quem é você, há quanto tempo atua, como é o atendimento…"></textarea>
            </label>
            <label class="field span-all">Produtos (um por linha)
              <textarea formControlName="products" rows="4" placeholder="Seguro de vida&#10;Vida em grupo para empresas&#10;Previdência"></textarea>
            </label>
            <label class="field">Telefone<input formControlName="phone" type="tel" placeholder="(21) 3333-4444" /></label>
            <label class="field">E-mail<input formControlName="email" type="email" /></label>
            <label class="field span-all">Endereço<input formControlName="address" placeholder="Rua, número, bairro, cidade/UF" /></label>
            <label class="field">Horário de atendimento<input formControlName="hours" placeholder="Seg a sex, 9h às 18h" /></label>
            <label class="field">Cor principal
              <span class="color"><input type="color" formControlName="accentColor" /> <code>{{ form.controls.accentColor.value }}</code></span>
            </label>
            <label class="field">Instagram<input formControlName="instagram" placeholder="@corretora" /></label>
            <label class="field">Facebook<input formControlName="facebook" placeholder="corretora ou link" /></label>
            <label class="field">LinkedIn<input formControlName="linkedIn" placeholder="seu-perfil ou link" /></label>
            <label class="field">Registro SUSEP<input formControlName="susep" placeholder="Ex.: 202012345" /></label>
            <label class="field">CNPJ (opcional)<input formControlName="cnpj" /></label>
            <label class="field span-all">Endereço do site (domínio)
              <input formControlName="host" placeholder="ex.: argleidson.usevitta.duckdns.org" />
              <span class="hint">Quem abrir esse endereço vê a home da corretora. Ele precisa apontar para o IP do servidor
                (subdomínios de <code>usevitta.duckdns.org</code> já apontam). O HTTPS é criado sozinho no primeiro acesso.
                Sem domínio, a home fica em <code>/corretora</code>.</span>
            </label>
          </div>
          <p class="muted small">A SUSEP pede o número de registro do corretor na publicidade. Ele aparece no rodapé do site.</p>

          @if (error(); as message) {
            <div class="alert alert--danger"><vt-icon name="alert" [size]="18" /><span>{{ message }}</span></div>
          }
          <div class="form-actions">
            <a class="btn" href="/corretora?previa=1" target="_blank" rel="noopener">Ver prévia</a>
            <button class="btn btn--primary" type="submit" [disabled]="saving()"><vt-icon name="check" [size]="16" /> {{ saving() ? 'Salvando…' : 'Salvar' }}</button>
          </div>
        </form>
      }
    </section>
  `,
  styles: `
    .block { display: flex; flex-direction: column; gap: 1rem; max-width: 720px; }
    form.block { max-width: none; }
    .block h2 { display: flex; align-items: center; gap: 0.5rem; margin: 0; }
    .intro { margin: -0.25rem 0 0; }
    .switch { display: flex; align-items: center; gap: 0.6rem; font-weight: 550; cursor: pointer;
      input { width: 18px; height: 18px; accent-color: var(--vt-primary); } }
    .images { display: grid; gap: 1rem; grid-template-columns: minmax(0, 1fr) minmax(0, 2fr); }
    .image { display: flex; flex-direction: column; gap: 0.4rem; }
    .image__label { font-weight: 600; font-size: 0.875rem; }
    .image__preview { height: 110px; border: 1.5px dashed var(--vt-border); border-radius: var(--vt-radius-sm); display: grid; place-items: center;
      overflow: hidden; background: var(--vt-surface-2);
      img { max-width: 100%; max-height: 100%; object-fit: contain; } }
    .image__preview--cover img { width: 100%; height: 100%; object-fit: cover; }
    .image__actions { display: flex; gap: 0.4rem; label { cursor: pointer; } }
    .color { display: flex; align-items: center; gap: 0.5rem; input { width: 48px; height: 36px; padding: 2px; } }
    code { font-size: 0.85em; }
    .form-actions { display: flex; gap: 0.6rem; justify-content: flex-end; }
    @media (max-width: 600px) { .images { grid-template-columns: minmax(0, 1fr); } }
  `,
})
export class SiteSettings {
  private readonly api = inject(SettingsApi);
  private readonly publicApi = inject(PublicApi);
  private readonly toast = inject(ToastService);

  protected readonly kinds: { kind: SiteImageKind; label: string; hint: string }[] = [
    { kind: 'logo', label: 'Logo', hint: 'PNG com fundo transparente fica melhor.' },
    { kind: 'cover', label: 'Imagem de capa', hint: 'Foto horizontal (ex.: família, você atendendo). O texto fica por cima.' },
  ];

  protected readonly site = signal<SiteDto | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    enabled: [false],
    host: [''],
    about: [''],
    products: [''],
    address: [''],
    phone: [''],
    email: [''],
    hours: [''],
    instagram: [''],
    facebook: [''],
    linkedIn: [''],
    susep: [''],
    cnpj: [''],
    accentColor: ['#0f6e5c'],
  });

  constructor() {
    this.load();
  }

  private load(): void {
    this.api.getSite().subscribe({
      next: s => {
        this.site.set(s);
        this.form.reset({
          enabled: s.enabled, host: s.host ?? '', about: s.about ?? '', products: s.products.join('\n'), address: s.address ?? '',
          phone: s.phone ?? '', email: s.email ?? '', hours: s.hours ?? '', instagram: s.instagram ?? '', facebook: s.facebook ?? '',
          linkedIn: s.linkedIn ?? '', susep: s.susep ?? '', cnpj: s.cnpj ?? '', accentColor: s.accentColor,
        });
      },
      error: err => this.error.set(problemMessage(err, 'Não foi possível carregar o site.')),
    });
  }

  protected imageUrl(kind: SiteImageKind): string | null {
    const s = this.site();
    return s ? this.publicApi.siteImageUrl(kind, kind === 'logo' ? s.logoVersion : s.coverVersion) : null;
  }

  protected save(): void {
    const v = this.form.getRawValue();
    const text = (x: string) => x.trim() || null;
    this.saving.set(true);
    this.error.set(null);
    this.api.saveSite({
      enabled: v.enabled, host: text(v.host), about: text(v.about), address: text(v.address), phone: text(v.phone), email: text(v.email),
      hours: text(v.hours), instagram: text(v.instagram), facebook: text(v.facebook), linkedIn: text(v.linkedIn), susep: text(v.susep),
      cnpj: text(v.cnpj), products: v.products.split('\n').map(p => p.trim()).filter(Boolean), accentColor: v.accentColor,
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success(v.enabled ? 'Site salvo e no ar.' : 'Site salvo (desligado).');
        this.load();
      },
      error: err => {
        this.saving.set(false);
        this.error.set(problemMessage(err, 'Não foi possível salvar.'));
      },
    });
  }

  protected async upload(kind: SiteImageKind, event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    try {
      const blob = await resize(file, kind === 'logo' ? 400 : 1600);
      this.api.uploadSiteImage(kind, blob).subscribe({
        next: () => {
          this.toast.success('Imagem atualizada.');
          this.load();
        },
        error: err => this.toast.error(err, 'Não foi possível enviar a imagem.'),
      });
    } catch {
      this.toast.error(null, 'Não foi possível ler a imagem.');
    }
  }

  protected removeImage(kind: SiteImageKind): void {
    this.api.deleteSiteImage(kind).subscribe({
      next: () => this.load(),
      error: err => this.toast.error(err, 'Não foi possível remover.'),
    });
  }
}
