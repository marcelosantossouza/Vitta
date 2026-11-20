import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '@vitta/core';
import { Icon, IconName } from '../shared/icon';
import { Logo } from '../shared/logo';

interface NavItem {
  path: string;
  label: string;
  icon: IconName;
}

@Component({
  selector: 'vt-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Icon, Logo],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  private readonly auth = inject(AuthService);

  protected readonly menuOpen = signal(false);
  protected readonly user = this.auth.currentUser;
  protected readonly initials = computed(() =>
    (this.user()?.name ?? '?')
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map(part => part[0]!.toUpperCase())
      .join(''),
  );

  protected readonly nav: NavItem[] = [
    { path: '/painel', label: 'Painel', icon: 'dashboard' },
    { path: '/clientes', label: 'Clientes', icon: 'users' },
    { path: '/apolices', label: 'Apólices', icon: 'policy' },
    { path: '/prospeccao', label: 'Prospecção', icon: 'target' },
    { path: '/carteira', label: 'Carteira', icon: 'folder' },
    { path: '/modelos', label: 'Modelos de planilha', icon: 'sheet' },
    { path: '/seguradoras', label: 'Seguradoras', icon: 'building' },
  ];

  protected toggleMenu(): void {
    this.menuOpen.update(open => !open);
  }

  protected closeMenu(): void {
    this.menuOpen.set(false);
  }

  protected logout(): void {
    this.auth.logout();
  }
}
