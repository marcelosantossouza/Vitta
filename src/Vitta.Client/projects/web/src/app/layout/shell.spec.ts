import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AUTH_STORAGE_KEY } from '@vitta/core';
import { Shell } from './shell';

describe('Shell', () => {
  beforeEach(() => {
    localStorage.setItem(
      AUTH_STORAGE_KEY,
      JSON.stringify({
        accessToken: 't',
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
        user: { id: '1', name: 'Carlos Souza', email: 'carlos@vitta.local' },
      }),
    );
  });

  afterEach(() => localStorage.clear());

  it('renderiza o menu e as iniciais do usuario', async () => {
    await TestBed.configureTestingModule({
      imports: [Shell],
      providers: [provideRouter([]), provideHttpClient()],
    }).compileComponents();

    const fixture = TestBed.createComponent(Shell);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelectorAll('nav a').length).toBe(8);
    expect(el.querySelector('.sidebar__user strong')?.textContent).toContain('Carlos Souza');
    expect(el.querySelector('.avatar')?.textContent?.trim()).toBe('CS');
  });
});
