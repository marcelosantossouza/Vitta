import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Shell } from './shell';

describe('Shell', () => {
  it('renderiza os itens de menu', async () => {
    await TestBed.configureTestingModule({ imports: [Shell], providers: [provideRouter([])] }).compileComponents();
    const fixture = TestBed.createComponent(Shell);
    fixture.detectChanges();
    const links = (fixture.nativeElement as HTMLElement).querySelectorAll('nav a');
    expect(links.length).toBe(5);
  });
});
