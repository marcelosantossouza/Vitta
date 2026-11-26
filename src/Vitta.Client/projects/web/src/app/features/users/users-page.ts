import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  formatDate, MIN_PASSWORD_LENGTH, problemMessage, USER_ROLE_HINTS, USER_ROLE_LABELS, UserListItemDto, UserRole, UsersApi,
} from '@vitta/core';
import { Icon } from '../../shared/icon';
import { ToastService } from '../../shared/toast.service';

type Dialog = { mode: 'create' } | { mode: 'edit'; user: UserListItemDto } | { mode: 'password'; user: UserListItemDto };

/** Senha provisoria legivel (sem 0/O, 1/l/I), gerada no navegador com crypto seguro. */
function generatePassword(length = 12): string {
  const chars = 'abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ23456789';
  const bytes = crypto.getRandomValues(new Uint32Array(length));
  return Array.from(bytes, b => chars[b % chars.length]).join('');
}

@Component({
  selector: 'vt-users-page',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './users-page.html',
  styleUrl: './users-page.scss',
})
export class UsersPage {
  private readonly api = inject(UsersApi);
  private readonly toast = inject(ToastService);

  protected readonly roles: UserRole[] = ['Admin', 'Broker', 'Assistant'];
  protected readonly roleLabels = USER_ROLE_LABELS;
  protected readonly roleHints = USER_ROLE_HINTS;
  protected readonly min = MIN_PASSWORD_LENGTH;
  protected readonly location = window.location.origin;

  protected readonly users = signal<UserListItemDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly dialog = signal<Dialog | null>(null);
  protected readonly busy = signal(false);
  protected readonly dialogError = signal<string | null>(null);
  /** Senha provisoria recem-definida, mostrada uma vez para o admin repassar. */
  protected readonly issued = signal<{ name: string; email: string; password: string } | null>(null);

  // Campos do dialogo
  protected readonly name = signal('');
  protected readonly email = signal('');
  protected readonly role = signal<UserRole>('Broker');
  protected readonly password = signal('');

  constructor() {
    this.load();
  }

  protected load(): void {
    this.api.list().subscribe({
      next: list => {
        this.users.set(list);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.toast.error(err, 'Não foi possível carregar os usuários.');
      },
    });
  }

  protected lastLogin(u: UserListItemDto): string {
    return u.lastLoginAt ? formatDate(u.lastLoginAt) : 'nunca entrou';
  }

  protected openCreate(): void {
    this.name.set('');
    this.email.set('');
    this.role.set('Broker');
    this.password.set(generatePassword());
    this.open({ mode: 'create' });
  }

  protected openEdit(user: UserListItemDto): void {
    this.name.set(user.name);
    this.role.set(user.role);
    this.open({ mode: 'edit', user });
  }

  protected openPassword(user: UserListItemDto): void {
    this.password.set(generatePassword());
    this.open({ mode: 'password', user });
  }

  private open(d: Dialog): void {
    this.dialogError.set(null);
    this.issued.set(null);
    this.dialog.set(d);
  }

  protected close(): void {
    this.dialog.set(null);
    this.busy.set(false);
  }

  protected regenerate(): void {
    this.password.set(generatePassword());
  }

  protected async copy(text: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.toast.success('Copiado.');
    } catch {
      this.toast.error('Não foi possível copiar; selecione e copie manualmente.');
    }
  }

  protected save(): void {
    const d = this.dialog();
    if (!d) return;
    if ((d.mode === 'create' || d.mode === 'password') && this.password().length < MIN_PASSWORD_LENGTH) {
      this.dialogError.set(`A senha provisória precisa ter ao menos ${MIN_PASSWORD_LENGTH} caracteres.`);
      return;
    }
    if (d.mode !== 'password' && !this.name().trim()) {
      this.dialogError.set('Informe o nome.');
      return;
    }
    this.busy.set(true);
    this.dialogError.set(null);

    const done = (message: string, issued?: { name: string; email: string; password: string }) => {
      this.busy.set(false);
      this.toast.success(message);
      this.load();
      if (issued) {
        this.issued.set(issued);
      } else {
        this.close();
      }
    };
    const fail = (err: unknown) => {
      this.busy.set(false);
      this.dialogError.set(problemMessage(err, 'Não foi possível salvar.'));
    };

    if (d.mode === 'create') {
      const request = { name: this.name().trim(), email: this.email().trim(), role: this.role(), temporaryPassword: this.password() };
      this.api.create(request).subscribe({
        next: () => done('Usuário criado.', { name: request.name, email: request.email.toLowerCase(), password: request.temporaryPassword }),
        error: fail,
      });
    } else if (d.mode === 'edit') {
      this.api.update(d.user.id, { name: this.name().trim(), role: this.role() }).subscribe({ next: () => done('Usuário atualizado.'), error: fail });
    } else {
      const password = this.password();
      this.api.resetPassword(d.user.id, password).subscribe({
        next: () => done('Senha provisória definida.', { name: d.user.name, email: d.user.email, password }),
        error: fail,
      });
    }
  }

  protected toggleActive(user: UserListItemDto): void {
    const action = user.isActive ? 'Desativar' : 'Reativar';
    if (!confirm(`${action} o acesso de ${user.name}?${user.isActive ? ' A pessoa sai do sistema na hora.' : ''}`)) return;
    this.api.setActive(user.id, !user.isActive).subscribe({
      next: () => {
        this.toast.success(user.isActive ? 'Acesso desativado.' : 'Acesso reativado.');
        this.load();
      },
      error: err => this.toast.error(err, 'Não foi possível alterar o acesso.'),
    });
  }

  /** Texto pronto para mandar por WhatsApp/e-mail (sem a senha no mesmo canal, se possivel). */
  protected instructions(i: { name: string; email: string; password: string }): string {
    return `Olá, ${i.name.split(' ')[0]}! Seu acesso à Vitta: ${location.origin} — login: ${i.email} — senha provisória: ${i.password} (você vai trocá-la no primeiro acesso).`;
  }
}
