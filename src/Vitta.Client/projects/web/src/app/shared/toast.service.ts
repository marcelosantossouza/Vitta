import { Injectable, signal } from '@angular/core';
import { problemMessage } from '@vitta/core';

export type ToastKind = 'success' | 'error' | 'info';

export interface Toast {
  id: number;
  kind: ToastKind;
  message: string;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  readonly toasts = signal<Toast[]>([]);

  success(message: string): void {
    this.push('success', message);
  }

  info(message: string): void {
    this.push('info', message);
  }

  error(messageOrError: unknown, fallback?: string): void {
    const message = typeof messageOrError === 'string' ? messageOrError : problemMessage(messageOrError, fallback);
    this.push('error', message, 6000);
  }

  dismiss(id: number): void {
    this.toasts.update(list => list.filter(t => t.id !== id));
  }

  private push(kind: ToastKind, message: string, duration = 3500): void {
    const id = this.nextId++;
    this.toasts.update(list => [...list.slice(-3), { id, kind, message }]);
    setTimeout(() => this.dismiss(id), duration);
  }
}
