import { Component, input } from '@angular/core';

@Component({
  selector: 'vt-logo',
  template: `
    <svg [attr.width]="size()" [attr.height]="size()" viewBox="0 0 40 40" role="img" aria-label="Vitta">
      <defs>
        <linearGradient id="vt-m-logo-g" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stop-color="#1fa386" />
          <stop offset="1" stop-color="#0b5a4b" />
        </linearGradient>
      </defs>
      <rect width="40" height="40" rx="11" fill="url(#vt-m-logo-g)" />
      <path d="M11 13.5 20 29l9-15.5" fill="none" stroke="#fff" stroke-width="3.6" stroke-linecap="round" stroke-linejoin="round" />
      <circle cx="20" cy="11" r="2.6" fill="#f5c76a" />
    </svg>
  `,
  styles: `:host { display: inline-flex; } svg { filter: drop-shadow(0 6px 14px rgba(0, 0, 0, 0.25)); }`,
})
export class Logo {
  readonly size = input(56);
}
