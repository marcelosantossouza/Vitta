import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { AUTH_STORAGE_KEY } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    localStorage.setItem(
      AUTH_STORAGE_KEY,
      JSON.stringify({ accessToken: 'tok', expiresAt: new Date(Date.now() + 3_600_000).toISOString(), user: { id: '1', name: 'A', email: 'a@a' } }),
    );
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => localStorage.clear());

  it('adiciona o Bearer nas chamadas da API', () => {
    http.get('/api/clients').subscribe();
    expect(controller.expectOne('/api/clients').request.headers.get('Authorization')).toBe('Bearer tok');
  });

  it('nao envia token para o login nem para URLs externas', () => {
    http.post('/api/auth/login', {}).subscribe();
    http.get('https://example.com/x').subscribe();
    expect(controller.expectOne('/api/auth/login').request.headers.has('Authorization')).toBe(false);
    expect(controller.expectOne('https://example.com/x').request.headers.has('Authorization')).toBe(false);
  });

  it('em 401 desloga e manda para /entrar', () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    http.get('/api/dashboard').subscribe({ error: () => undefined });
    controller.expectOne('/api/dashboard').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(localStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/entrar']);
  });
});
