import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService, USER_ROLE_LABELS } from '@vitta/core';
import { Icon, IconName } from '../shared/icon';
import { Logo } from '../shared/logo';

interface NavItem {
  path: string;
  label: string;
  icon: IconName;
  /** So aparece para administradores (a API tambem exige o perfil). */
  adminOnly?: boolean;
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
  private readonly allGroups: NavGroup[] = [
    { title: null, items: [{ path: '/painel', label: 'Painel', icon: 'dashboard' }] },
    {
      title: 'Carteira',
      items: [
        { path: '/clientes', label: 'Clientes', icon: 'users' },
        { path: '/apolices', label: 'Apólices', icon: 'policy' },
        { path: '/carteira', label: 'Importações', icon: 'upload' },
      ],
    },
    {
      title: 'Vendas',
      items: [
        { path: '/prospeccao', label: 'Prospecção', icon: 'target' },
        { path: '/indicacoes', label: 'Indicações', icon: 'link', adminOnly: true },
      ],
    },
    {
      title: 'Cadastros',
      items: [
        { path: '/modelos', label: 'Modelos de planilha', icon: 'sheet' },
        { path: '/seguradoras', label: 'Seguradoras', icon: 'building' },
      ],
    },
  ];
  /** Rodape: so para administradores (a API tambem exige o perfil). */
  protected readonly adminItems: NavItem[] = [
    { path: '/usuarios', label: 'Usuários', icon: 'shield' },
    { path: '/configuracoes', label: 'Configurações', icon: 'settings' },
  ];
  protected readonly isAdmin = this.auth.isAdmin;
  protected readonly groups = computed(() =>
    this.allGroups.map(g => ({ ...g, items: g.items.filter(i => !i.adminOnly || this.isAdmin()) })),
  );
  protected readonly roleLabel = computed(() => {
    const role = this.auth.role();
    return role ? USER_ROLE_LABELS[role] : '';
  });

  constructor() {
    // O perfil pode ter mudado desde o login (outro admin editou): busca o atual.
    this.auth.refreshUser();
  }

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
