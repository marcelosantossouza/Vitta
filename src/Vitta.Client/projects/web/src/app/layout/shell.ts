import { Component, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

interface NavItem {
  path: string;
  label: string;
  icon: string;
}

@Component({
  selector: 'vt-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  protected readonly menuOpen = signal(false);

  protected readonly nav: NavItem[] = [
    { path: '/dashboard', label: 'Painel', icon: '📊' },
    { path: '/clientes', label: 'Clientes', icon: '👥' },
    { path: '/prospeccao', label: 'Prospecção', icon: '🎯' },
    { path: '/carteira', label: 'Carteira', icon: '📁' },
    { path: '/seguradoras', label: 'Seguradoras', icon: '🏢' },
  ];

  protected toggleMenu(): void {
    this.menuOpen.update(open => !open);
  }

  protected closeMenu(): void {
    this.menuOpen.set(false);
  }
}
