import { Routes } from '@angular/router';
import { adminGuard, authGuard, guestGuard } from '@vitta/core';
import { Shell } from './layout/shell';

export const routes: Routes = [
  // Tela de abertura (marca + login). Quem ja esta logado vai direto para o painel.
  {
    path: '',
    pathMatch: 'full',
    canActivate: [guestGuard],
    title: 'Vitta — Gestão da sua corretora',
    loadComponent: () => import('./features/landing/landing').then(m => m.Landing),
  },
  {
    path: 'entrar',
    canActivate: [guestGuard],
    title: 'Entrar — Vitta',
    loadComponent: () => import('./features/landing/landing').then(m => m.Landing),
  },
  // Publica (sem login): pagina de captacao da corretora. ?origem=instagram etc.
  {
    path: 'simular',
    title: 'Seguro de vida — fale com um corretor',
    loadComponent: () => import('./features/lead-page/lead-page').then(m => m.LeadPage),
  },
  // Fora do menu: troca obrigatoria de senha (senha provisoria) e troca voluntaria pelo menu do usuario.
  {
    path: 'trocar-senha',
    canActivate: [authGuard],
    title: 'Trocar senha — Vitta',
    loadComponent: () => import('./features/account/change-password').then(m => m.ChangePassword),
  },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: 'painel', title: 'Painel — Vitta', loadComponent: () => import('./features/dashboard/dashboard').then(m => m.Dashboard) },
      { path: 'clientes', title: 'Clientes — Vitta', loadComponent: () => import('./features/clients/client-list').then(m => m.ClientList) },
      { path: 'clientes/novo', title: 'Novo cliente — Vitta', loadComponent: () => import('./features/clients/client-form').then(m => m.ClientForm) },
      { path: 'clientes/:id', title: 'Cliente — Vitta', loadComponent: () => import('./features/clients/client-detail').then(m => m.ClientDetail) },
      { path: 'clientes/:id/editar', title: 'Editar cliente — Vitta', loadComponent: () => import('./features/clients/client-form').then(m => m.ClientForm) },
      { path: 'apolices', title: 'Apólices — Vitta', loadComponent: () => import('./features/policies/policy-list').then(m => m.PolicyList) },
      { path: 'prospeccao', title: 'Prospecção — Vitta', loadComponent: () => import('./features/prospects/prospect-board').then(m => m.ProspectBoard) },
      { path: 'prospeccao/lista', title: 'Prospecção · Lista — Vitta', loadComponent: () => import('./features/prospects/prospect-list').then(m => m.ProspectList) },
      { path: 'prospeccao/importacoes', title: 'Prospecção · Importações — Vitta', loadComponent: () => import('./features/prospects/prospect-imports').then(m => m.ProspectImports) },
      { path: 'carteira', title: 'Carteira — Vitta', loadComponent: () => import('./features/portfolio/portfolio-page').then(m => m.PortfolioPage) },
      { path: 'carteira/:id', title: 'Importação — Vitta', loadComponent: () => import('./features/portfolio/import-detail').then(m => m.ImportDetail) },
      { path: 'modelos', title: 'Modelos de planilha — Vitta', loadComponent: () => import('./features/import-templates/template-list').then(m => m.TemplateList) },
      { path: 'modelos/novo', title: 'Novo modelo — Vitta', loadComponent: () => import('./features/import-templates/template-wizard').then(m => m.TemplateWizard) },
      { path: 'modelos/:id', title: 'Editar modelo — Vitta', loadComponent: () => import('./features/import-templates/template-wizard').then(m => m.TemplateWizard) },
      { path: 'seguradoras', title: 'Seguradoras — Vitta', loadComponent: () => import('./features/insurers/insurers').then(m => m.Insurers) },
      { path: 'configuracoes', title: 'Configurações — Vitta', canActivate: [adminGuard], loadComponent: () => import('./features/settings/settings-page').then(m => m.SettingsPage) },
      { path: 'usuarios', title: 'Usuários — Vitta', canActivate: [adminGuard], loadComponent: () => import('./features/users/users-page').then(m => m.UsersPage) },
    ],
  },
  { path: '**', redirectTo: '' },
];
