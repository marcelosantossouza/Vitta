import { ChangeDetectionStrategy, Component, input } from '@angular/core';

let nextLogoId = 0;

/** Marca do Vitta: um "V" que tambem lembra um escudo/protecao, com um ponto de vida. */
@Component({
  selector: 'vt-logo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg [attr.width]="size()" [attr.height]="size()" viewBox="0 0 40 40" role="img" aria-label="Vitta">
      <defs>
        <linearGradient [attr.id]="gradientId" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stop-color="#1fa386" />
          <stop offset="1" stop-color="#0b5a4b" />
        </linearGradient>
      </defs>
      <rect width="40" height="40" rx="11" [attr.fill]="'url(#' + gradientId + ')'" />
      <path d="M11 13.5 20 29l9-15.5" fill="none" stroke="#fff" stroke-width="3.6" stroke-linecap="round" stroke-linejoin="round" />
      <circle cx="20" cy="11" r="2.6" fill="#f5c76a" />
    </svg>
    @if (showText()) {
      <span class="wordmark">Vitta</span>
    }
  `,
  styles: `
    :host { display: inline-flex; align-items: center; gap: 0.6rem; }
    svg { flex-shrink: 0; filter: drop-shadow(0 4px 10px rgba(11, 90, 75, 0.25)); }
    .wordmark { font-weight: 750; font-size: 1.35rem; letter-spacing: -0.02em; }
  `,
})
export class Logo {
  /** id unico por instancia: com ids repetidos, um logo oculto (display:none) "apaga" o gradiente dos outros. */
  protected readonly gradientId = `vt-logo-g-${nextLogoId++}`;

  readonly size = input(36);
  readonly showText = input(true);
}
