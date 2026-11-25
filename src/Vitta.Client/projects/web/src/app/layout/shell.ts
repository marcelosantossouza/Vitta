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

interface NavGroup {
  /** Titulo do grupo; nulo = sem titulo (Painel, no topo). */
  title: string | null;
  items: NavItem[];
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

  /** Menu por assunto: o dia a dia em cima, cadastros de apoio embaixo; Configuracoes fica no rodape. */
  protected readonly groups: NavGroup[] = [
    { title: null, items: [{ path: '/painel', label: 'Painel', icon: 'dashboard' }] },
    {
      title: 'Carteira',
      items: [
        { path: '/clientes', label: 'Clientes', icon: 'users' },
        { path: '/apolices', label: 'Apólices', icon: 'policy' },
        { path: '/carteira', label: 'Importações', icon: 'upload' },
      ],
    },
    { title: 'Vendas', items: [{ path: '/prospeccao', label: 'Prospecção', icon: 'target' }] },
    {
      title: 'Cadastros',
      items: [
        { path: '/modelos', label: 'Modelos de planilha', icon: 'sheet' },
        { path: '/seguradoras', label: 'Seguradoras', icon: 'building' },
      ],
    },
  ];
  protected readonly settings: NavItem = { path: '/configuracoes', label: 'Configurações', icon: 'settings' };

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
