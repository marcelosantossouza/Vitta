import { Routes } from '@angular/router';
import { Tabs } from './tabs/tabs';

export const routes: Routes = [
  {
    path: 'tabs',
    component: Tabs,
    children: [
      { path: 'painel', loadComponent: () => import('./pages/dashboard/dashboard').then(m => m.DashboardPage) },
      { path: 'clientes', loadComponent: () => import('./pages/clients/clients').then(m => m.ClientsPage) },
      { path: 'prospeccao', loadComponent: () => import('./pages/prospects/prospects').then(m => m.ProspectsPage) },
      { path: 'carteira', loadComponent: () => import('./pages/portfolio/portfolio').then(m => m.PortfolioPage) },
      { path: '', pathMatch: 'full', redirectTo: 'painel' },
    ],
  },
  { path: '', pathMatch: 'full', redirectTo: 'tabs/painel' },
  { path: '**', redirectTo: 'tabs/painel' },
];
