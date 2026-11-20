import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { AuthApi } from '../api/auth-api';
import { AUTH_ROUTES } from '../config/api-config';
import { LoginRequest, LoginResponse, UserDto } from '../models/auth';

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

  /** Limpa a sessao e volta para a tela de entrada. */
  logout(redirect = true): void {
    this.clear();
    if (redirect) {
      void this.router.navigate([this.routes.login]);
    }
  }

  private store(response: LoginResponse): void {
    const session: StoredSession = {
      accessToken: response.accessToken,
      expiresAt: response.expiresAt,
      user: response.user,
    };
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
