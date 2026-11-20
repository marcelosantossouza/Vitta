import { inject, Injectable } from '@angular/core';
import { ToastController } from '@ionic/angular';
import { problemMessage } from '@vitta/core';

/** Toasts padronizados do app mobile. */
@Injectable({ providedIn: 'root' })
export class Feedback {
  private readonly toasts = inject(ToastController);

  async success(message: string): Promise<void> {
    const toast = await this.toasts.create({ message, duration: 2500, color: 'success', position: 'bottom' });
    await toast.present();
  }

  async error(error: unknown, fallback?: string): Promise<void> {
    const message = typeof error === 'string' ? error : problemMessage(error, fallback);
    const toast = await this.toasts.create({ message, duration: 4000, color: 'danger', position: 'bottom' });
    await toast.present();
  }
}
