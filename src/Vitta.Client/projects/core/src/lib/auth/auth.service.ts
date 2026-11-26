import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { AuthApi } from '../api/auth-api';
import { AUTH_ROUTES } from '../config/api-config';
import { ChangePasswordRequest, LoginRequest, LoginResponse, UserDto, UserRole } from '../models/auth';

export const AUTH_STORAGE_KEY = 'vitta.auth';

interface StoredSession {
  accessToken: string;
  expiresAt: string;
  user: UserDto;
}

function readSession(): StoredSession | null {
  try {
    const raw = localStorage.getItem(AUTH_STORAGE_KEY);
    if (!raw) return null;
    const session = JSON.parse(raw) as StoredSession;
    if (!session?.accessToken || !session.expiresAt || isExpired(session.expiresAt)) {
      localStorage.removeItem(AUTH_STORAGE_KEY);
      return null;
    }
    return session;
  } catch {
    return null;
  }
}

function isExpired(expiresAt: string): boolean {
  const time = Date.parse(expiresAt);
  return Number.isNaN(time) || time <= Date.now();
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(AuthApi);
  private readonly router = inject(Router);
  private readonly routes = inject(AUTH_ROUTES);
  private readonly session = signal<StoredSession | null>(readSession());

  readonly currentUser = computed(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => {
    const s = this.session();
    return !!s && !isExpired(s.expiresAt);
  });

  /** Perfil do usuario logado. Quem decide de verdade e a API; aqui so serve para esconder o que nao se aplica. */
  readonly role = computed<UserRole | null>(() => this.currentUser()?.role ?? null);
  readonly isAdmin = computed(() => this.role() === 'Admin');
  /** Assistentes nao excluem registros. */
  readonly canDelete = computed(() => this.role() === 'Admin' || this.role() === 'Broker');
  readonly mustChangePassword = computed(() => !!this.currentUser()?.mustChangePassword);

  /** Token valido atual, ou null se ausente/expirado. */
  get token(): string | null {
    const s = this.session();
    if (!s) return null;
    if (isExpired(s.expiresAt)) {
      this.clear();
      return null;
    }
    return s.accessToken;
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.api.login(request).pipe(tap(response => this.store(response)));
  }

  /** Atualiza nome/perfil a partir da API (perfil pode ter mudado desde o login). */
  refreshUser(): void {
    if (!this.isAuthenticated()) return;
    this.api.me().subscribe({ next: user => this.updateUser(user), error: () => undefined });
  }

  changePassword(request: ChangePasswordRequest): Observable<void> {
    return this.api.changePassword(request).pipe(
      tap(() => {
        const user = this.currentUser();
        if (user) this.updateUser({ ...user, mustChangePassword: false });
      }),
    );
  }

  private updateUser(user: UserDto): void {
    const s = this.session();
    if (!s) return;
    this.persist({ ...s, user });
  }

  /** Limpa a sessao e volta para a tela de entrada. */
  logout(redirect = true): void {
    this.clear();
    if (redirect) {
      void this.router.navigate([this.routes.login]);
    }
  }

  private store(response: LoginResponse): void {
    this.persist({ accessToken: response.accessToken, expiresAt: response.expiresAt, user: response.user });
  }

  private persist(session: StoredSession): void {
    this.session.set(session);
    try {
      localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session));
    } catch {
      /* storage indisponivel (modo privado): sessao fica so em memoria */
    }
  }

  private clear(): void {
    this.session.set(null);
    try {
      localStorage.removeItem(AUTH_STORAGE_KEY);
    } catch {
      /* ignore */
    }
  }
}
