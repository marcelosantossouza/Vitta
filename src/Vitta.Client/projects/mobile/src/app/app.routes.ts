import { Routes } from '@angular/router';
import { authGuard, guestGuard } from '@vitta/core';
import { Tabs } from './tabs/tabs';

export const routes: Routes = [
  {
    path: 'entrar',
    canActivate: [guestGuard],
    loadComponent: () => import('./pages/login/login').then(m => m.LoginPage),
  },
  {
    path: 'tabs',
    component: Tabs,
    canActivate: [authGuard],
    children: [
      { path: 'painel', loadComponent: () => import('./pages/dashboard/dashboard').then(m => m.DashboardPage) },
      { path: 'clientes', loadComponent: () => import('./pages/clients/clients').then(m => m.ClientsPage) },
      { path: 'clientes/:id', loadComponent: () => import('./pages/clients/client-detail').then(m => m.ClientDetailPage) },
      { path: 'prospeccao', loadComponent: () => import('./pages/prospects/prospects').then(m => m.ProspectsPage) },
      { path: 'carteira', loadComponent: () => import('./pages/portfolio/portfolio').then(m => m.PortfolioPage) },
      { path: '', pathMatch: 'full', redirectTo: 'painel' },
    ],
  },
  { path: '', pathMatch: 'full', redirectTo: 'tabs/painel' },
  { path: '**', redirectTo: 'tabs/painel' },
];
