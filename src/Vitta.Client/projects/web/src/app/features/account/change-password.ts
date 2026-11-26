import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService, MIN_PASSWORD_LENGTH, problemMessage } from '@vitta/core';
import { Icon } from '../../shared/icon';
import { Logo } from '../../shared/logo';
import { ToastService } from '../../shared/toast.service';

/**
 * Troca de senha. Obrigatoria no primeiro acesso com senha provisoria (o authGuard traz para ca);
 * opcional pelo menu do usuario.
 */
@Component({
  selector: 'vt-change-password',
  imports: [Icon, Logo, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wrap">
      <form class="card box" (submit)="$event.preventDefault(); submit()" novalidate>
        <vt-logo [size]="34" />
        <div>
          <h1>{{ forced() ? 'Defina sua senha' : 'Trocar senha' }}</h1>
          <p class="muted small">
            @if (forced()) {
              Olá, {{ firstName() }}! Você entrou com uma senha provisória. Escolha uma senha só sua para continuar.
            } @else {
              Digite a senha atual e escolha uma nova.
            }
          </p>
        </div>

        <label class="field">
          {{ forced() ? 'Senha provisória' : 'Senha atual' }}
          <input type="password" autocomplete="current-password" [value]="current()" (input)="current.set($any($event.target).value)" />
        </label>
        <label class="field">
          Nova senha
          <input type="password" autocomplete="new-password" [value]="next()" (input)="next.set($any($event.target).value)" />
          <span class="hint">Mínimo de {{ min }} caracteres. Evite reaproveitar senhas de outros serviços.</span>
        </label>
        <label class="field">
          Repita a nova senha
          <input type="password" autocomplete="new-password" [value]="confirm()" (input)="confirm.set($any($event.target).value)" />
        </label>

        @if (error(); as message) {
          <div class="alert alert--danger"><vt-icon name="alert" [size]="18" /><span>{{ message }}</span></div>
        }

        <div class="actions">
          @if (forced()) {
            <button class="btn btn--ghost" type="button" (click)="auth.logout()">Sair</button>
          } @else {
            <a class="btn btn--ghost" routerLink="/painel">Cancelar</a>
          }
          <button class="btn btn--primary" type="submit" [disabled]="saving()">
            <vt-icon name="check" [size]="16" /> {{ saving() ? 'Salvando…' : 'Salvar senha' }}
          </button>
        </div>
      </form>
    </div>
  `,
  styles: `
    .wrap { min-height: 100dvh; display: grid; place-items: center; padding: 1rem; background: var(--vt-bg); }
    .box { width: min(420px, 100%); display: flex; flex-direction: column; gap: 1rem; }
    h1 { margin: 0.25rem 0 0.25rem; font-size: 1.35rem; }
    p { margin: 0; }
    .actions { display: flex; justify-content: space-between; gap: 0.6rem; }
  `,
})
export class ChangePassword {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly min = MIN_PASSWORD_LENGTH;
  protected readonly forced = this.auth.mustChangePassword;
  protected readonly firstName = computed(() => (this.auth.currentUser()?.name ?? '').split(' ')[0]);

  protected readonly current = signal('');
  protected readonly next = signal('');
  protected readonly confirm = signal('');
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected submit(): void {
    if (!this.current()) {
      this.error.set('Digite a senha atual.');
      return;
    }
    if (this.next().length < MIN_PASSWORD_LENGTH) {
      this.error.set(`A nova senha precisa ter ao menos ${MIN_PASSWORD_LENGTH} caracteres.`);
      return;
    }
    if (this.next() !== this.confirm()) {
      this.error.set('As duas senhas novas não são iguais.');
      return;
    }
    this.error.set(null);
    this.saving.set(true);
    this.auth.changePassword({ currentPassword: this.current(), newPassword: this.next() }).subscribe({
      next: () => {
        this.toast.success('Senha alterada.');
        void this.router.navigate(['/painel']);
      },
      error: err => {
        this.saving.set(false);
        this.error.set(problemMessage(err, 'Não foi possível trocar a senha.'));
      },
    });
  }
}
