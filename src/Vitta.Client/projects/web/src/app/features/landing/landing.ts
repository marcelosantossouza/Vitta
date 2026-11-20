import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService, problemMessage } from '@vitta/core';
import { Icon, IconName } from '../../shared/icon';
import { Logo } from '../../shared/logo';

interface Feature {
  icon: IconName;
  title: string;
  text: string;
}

@Component({
  selector: 'vt-landing',
  imports: [ReactiveFormsModule, Logo, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './landing.html',
  styleUrl: './landing.scss',
})
export class Landing {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly year = new Date().getFullYear();
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showPassword = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected readonly features: Feature[] = [
    { icon: 'folder', title: 'Carteira sob controle', text: 'Clientes e apólices de todas as seguradoras em um só lugar.' },
    { icon: 'trending-down', title: 'Alertas de saída', text: 'Suba a planilha mensal e veja quem entrou, saiu ou mudou.' },
    { icon: 'target', title: 'Prospecção', text: 'Funil de possíveis clientes com lembretes de follow-up.' },
  ];

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => {
        const back = this.route.snapshot.queryParamMap.get('voltar');
        void this.router.navigateByUrl(back && back.startsWith('/') ? back : '/painel');
      },
      error: err => {
        this.loading.set(false);
        this.error.set(
          (err as { status?: number })?.status === 401 ? 'E-mail ou senha inválidos.' : problemMessage(err, 'Não foi possível entrar.'),
        );
      },
    });
  }
}
