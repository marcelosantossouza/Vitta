import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Icon } from './icon';

/** Painel que sobe de baixo no celular (e vira janela no computador). */
@Component({
  selector: 'vt-sheet',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'closed.emit()' },
  template: `
    <div class="dialog-backdrop" (click)="closed.emit()">
      <div class="dialog sheet" role="dialog" aria-modal="true" [attr.aria-label]="title()" (click)="$event.stopPropagation()">
        <div class="dialog__header">
          <h2>{{ title() }}</h2>
          <button class="btn btn--ghost btn--icon" type="button" (click)="closed.emit()" aria-label="Fechar"><vt-icon name="x" /></button>
        </div>
        <ng-content />
      </div>
    </div>
  `,
  styles: `
    @media (max-width: 720px) {
      .dialog-backdrop { place-items: end center; padding: 0; }
      .sheet { width: 100%; border-radius: 18px 18px 0 0; max-height: 88dvh; padding-top: 0.6rem;
        padding-bottom: calc(1.2rem + env(safe-area-inset-bottom)); }
      .sheet::before { content: ''; display: block; width: 40px; height: 4px; border-radius: 4px; margin: 0 auto 0.8rem;
        background: var(--vt-border); }
    }
  `,
})
export class Sheet {
  readonly title = input.required<string>();
  readonly closed = output<void>();
}
