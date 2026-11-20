import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Icon, IconName } from './icon';

@Component({
  selector: 'vt-empty-state',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="empty">
      <span class="empty__icon"><vt-icon [name]="icon()" [size]="26" /></span>
      <strong>{{ title() }}</strong>
      @if (text()) {
        <p>{{ text() }}</p>
      }
      <ng-content />
    </div>
  `,
  styles: `
    .empty {
      display: flex; flex-direction: column; align-items: center; text-align: center; gap: 0.4rem;
      padding: 2rem 1rem; color: var(--vt-muted);
    }
    .empty__icon {
      display: grid; place-items: center; width: 52px; height: 52px; border-radius: 50%;
      background: var(--vt-primary-soft); color: var(--vt-primary); margin-bottom: 0.25rem;
    }
    strong { color: var(--vt-text); }
    p { margin: 0; max-width: 42ch; font-size: 0.9rem; }
  `,
})
export class EmptyState {
  readonly icon = input<IconName>('info');
  readonly title = input.required<string>();
  readonly text = input<string>('');
}
