import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AUTH_STORAGE_KEY, AuthService } from './auth.service';

const user = { id: '1', name: 'Carlos Souza', email: 'carlos@vitta.local' };

function inFuture(minutes = 60): string {
  return new Date(Date.now() + minutes * 60_000).toISOString();
}

describe('AuthService', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  function setup(): { service: AuthService; http: HttpTestingController } {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    return { service: TestBed.inject(AuthService), http: TestBed.inject(HttpTestingController) };
  }

  it('comeca deslogado sem sessao salva', () => {
    const { service } = setup();
    expect(service.isAuthenticated()).toBe(false);
    expect(service.token).toBeNull();
  });

  it('login guarda token, usuario e persiste no localStorage', () => {
    const { service, http } = setup();
    service.login({ email: user.email, password: 'x' }).subscribe();
    http.expectOne('/api/auth/login').flush({ accessToken: 'abc', expiresAt: inFuture(), user });

    expect(service.isAuthenticated()).toBe(true);
    expect(service.token).toBe('abc');
    expect(service.currentUser()?.name).toBe('Carlos Souza');
    expect(JSON.parse(localStorage.getItem(AUTH_STORAGE_KEY)!).accessToken).toBe('abc');
  });

  it('restaura sessao valida do localStorage', () => {
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify({ accessToken: 'saved', expiresAt: inFuture(), user }));
    const { service } = setup();
    expect(service.isAuthenticated()).toBe(true);
    expect(service.token).toBe('saved');
  });

  it('descarta sessao expirada', () => {
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify({ accessToken: 'old', expiresAt: inFuture(-5), user }));
    const { service } = setup();
    expect(service.isAuthenticated()).toBe(false);
    expect(service.token).toBeNull();
    expect(localStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
  });

  it('logout limpa a sessao e volta para /entrar', () => {
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify({ accessToken: 'saved', expiresAt: inFuture(), user }));
    const { service } = setup();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    service.logout();

    expect(service.isAuthenticated()).toBe(false);
    expect(localStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/entrar']);
  });
});
