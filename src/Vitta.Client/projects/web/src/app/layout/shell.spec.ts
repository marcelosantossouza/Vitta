import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AUTH_STORAGE_KEY } from '@vitta/core';
import { Shell } from './shell';

describe('Shell', () => {
  const login = (role: string) =>
    localStorage.setItem(
      AUTH_STORAGE_KEY,
      JSON.stringify({
        accessToken: 't',
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
        user: { id: '1', name: 'Carlos Souza', email: 'carlos@vitta.local', role, mustChangePassword: false },
      }),
    );

  const render = async () => {
    await TestBed.configureTestingModule({
      imports: [Shell],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    const fixture = TestBed.createComponent(Shell);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  afterEach(() => localStorage.clear());

  it('renderiza o menu e as iniciais do usuario', async () => {
    login('Admin');
    const el = await render();

    expect(el.querySelectorAll('nav a').length).toBe(7);
    expect(Array.from(el.querySelectorAll('.group__title')).map(t => t.textContent?.trim())).toEqual(['Carteira', 'Vendas', 'Cadastros']);
    const admin = Array.from(el.querySelectorAll('.sidebar__footer a.settings')).map(a => a.textContent?.trim());
    expect(admin).toEqual(['Usuários', 'Configurações']);
    expect(el.querySelector('.sidebar__user small')?.textContent).toContain('Administrador');
    expect(el.querySelector('.sidebar__user strong')?.textContent).toContain('Carlos Souza');
    expect(el.querySelector('.avatar')?.textContent?.trim()).toBe('CS');
  });

  it('esconde Usuarios e Configuracoes de quem nao e administrador', async () => {
    login('Assistant');
    const el = await render();

    expect(el.querySelectorAll('.sidebar__footer a.settings').length).toBe(0);
    expect(el.querySelector('.sidebar__user small')?.textContent).toContain('Assistente');
    expect(el.querySelector('a[href="/trocar-senha"]')).not.toBeNull();
  });
});
