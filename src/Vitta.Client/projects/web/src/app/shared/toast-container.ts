import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Icon } from './icon';
import { ToastService } from './toast.service';

@Component({
  selector: 'vt-toast-container',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="toasts" aria-live="polite">
      @for (toast of service.toasts(); track toast.id) {
        <div class="toast" [class]="'toast toast--' + toast.kind" role="status">
          <vt-icon [name]="toast.kind === 'success' ? 'check' : toast.kind === 'error' ? 'alert' : 'info'" [size]="18" />
          <span>{{ toast.message }}</span>
          <button type="button" class="close" (click)="service.dismiss(toast.id)" aria-label="Fechar">
            <vt-icon name="x" [size]="16" />
          </button>
        </div>
      }
    </div>
  `,
  styles: `
    .toasts {
      position: fixed; right: 1rem; bottom: 1rem; z-index: 100;
      display: flex; flex-direction: column; gap: 0.5rem; max-width: min(420px, calc(100vw - 2rem));
    }
    .toast {
      display: flex; align-items: flex-start; gap: 0.6rem; padding: 0.8rem 0.9rem;
      border-radius: 12px; background: var(--vt-surface); color: var(--vt-text);
      box-shadow: var(--vt-shadow-lg); border-left: 4px solid var(--vt-primary);
      animation: toast-in 0.25s ease-out; font-size: 0.925rem;
    }
    .toast span { flex: 1; }
    .toast--success { border-left-color: var(--vt-success); vt-icon { color: var(--vt-success); } }
    .toast--error { border-left-color: var(--vt-danger); vt-icon { color: var(--vt-danger); } }
    .toast--info vt-icon { color: var(--vt-primary); }
    .close { background: none; border: 0; padding: 0; cursor: pointer; color: var(--vt-muted); }
    @keyframes toast-in { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: none; } }
  `,
})
export class ToastContainer {
  protected readonly service = inject(ToastService);
}
