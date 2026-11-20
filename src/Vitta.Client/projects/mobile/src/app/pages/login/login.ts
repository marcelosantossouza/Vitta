import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { IonButton, IonContent, IonInput, IonInputPasswordToggle, IonSpinner, IonText } from '@ionic/angular';
import { AuthService, problemMessage } from '@vitta/core';
import { Logo } from '../../shared/logo';

@Component({
  selector: 'vt-login-page',
  imports: [ReactiveFormsModule, IonContent, IonInput, IonInputPasswordToggle, IonButton, IonSpinner, IonText, Logo],
  template: `
    <ion-content [fullscreen]="true">
      <div class="login">
        <div class="brand">
          <vt-logo [size]="64" />
          <h1>Vitta</h1>
          <p>Gestão da sua corretora de seguros de vida</p>
        </div>

        <form class="card" [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <h2>Entrar</h2>
          <ion-input label="E-mail" labelPlacement="floating" fill="outline" type="email" formControlName="email"
            autocomplete="username" inputmode="email" errorText="Informe um e-mail válido." />
          <ion-input label="Senha" labelPlacement="floating" fill="outline" type="password" formControlName="password"
            autocomplete="current-password" errorText="Informe a senha.">
            <ion-input-password-toggle slot="end" />
          </ion-input>

          @if (error(); as message) {
            <ion-text color="danger"><p class="error">{{ message }}</p></ion-text>
          }

          <ion-button expand="block" type="submit" [disabled]="loading()">
            @if (loading()) { <ion-spinner name="crescent" /> } @else { Entrar }
          </ion-button>
        </form>
      </div>
    </ion-content>
  `,
  styles: `
    ion-content {
      --background: radial-gradient(600px 400px at 0% 0%, rgba(31, 163, 134, 0.45), transparent 60%),
        linear-gradient(165deg, #0f2924 0%, #0b3a31 60%, #0d4a3d 100%);
    }
    .login {
      min-height: 100%; display: flex; flex-direction: column; justify-content: center; gap: 2rem;
      padding: max(2rem, env(safe-area-inset-top)) 1.25rem 2rem;
    }
    .brand { text-align: center; color: #fff; animation: rise 0.6s ease-out both; }
    .brand h1 { margin: 0.75rem 0 0.25rem; font-size: 2.2rem; font-weight: 800; letter-spacing: -0.03em; }
    .brand p { margin: 0; color: #b5d3ca; }
    .card {
      display: flex; flex-direction: column; gap: 1rem; padding: 1.5rem; border-radius: 22px;
      background: #fff; box-shadow: 0 30px 60px -20px rgba(0, 0, 0, 0.45); animation: rise 0.6s 0.12s ease-out both;
    }
    .card h2 { margin: 0; font-size: 1.3rem; font-weight: 700; color: #16211e; }
    .error { margin: 0; font-size: 0.9rem; }
    ion-button { --border-radius: 12px; min-height: 48px; margin-top: 0.25rem; }
    @keyframes rise { from { opacity: 0; transform: translateY(14px); } to { opacity: 1; transform: none; } }
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => {
        this.loading.set(false);
        const back = this.route.snapshot.queryParamMap.get('voltar');
        void this.router.navigateByUrl(back && back.startsWith('/tabs') ? back : '/tabs/painel', { replaceUrl: true });
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err?.status === 401 ? 'E-mail ou senha inválidos.' : problemMessage(err, 'Não foi possível entrar.'));
      },
    });
  }
}
