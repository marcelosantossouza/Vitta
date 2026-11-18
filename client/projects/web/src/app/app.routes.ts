import { Routes } from '@angular/router';
import { Shell } from './layout/shell';

export const routes: Routes = [
  {
    path: '',
    component: Shell,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', title: 'Painel', loadComponent: () => import('./features/dashboard/dashboard').then(m => m.Dashboard) },
      { path: 'clientes', title: 'Clientes', loadComponent: () => import('./features/clients/client-list').then(m => m.ClientList) },
      { path: 'clientes/novo', title: 'Novo cliente', loadComponent: () => import('./features/clients/client-form').then(m => m.ClientForm) },
      { path: 'prospeccao', title: 'Prospeccao', loadComponent: () => import('./features/prospects/prospects').then(m => m.Prospects) },
      { path: 'carteira', title: 'Carteira', loadComponent: () => import('./features/portfolio/portfolio').then(m => m.Portfolio) },
      { path: 'seguradoras', title: 'Seguradoras', loadComponent: () => import('./features/insurers/insurers').then(m => m.Insurers) },
    ],
  },
  { path: '**', redirectTo: '' },
];
