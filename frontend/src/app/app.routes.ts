import { Routes } from '@angular/router';
import { adminGuard } from './core/auth/admin.guard';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard) },
  { path: 'rides', loadComponent: () => import('./features/rides/rides').then((m) => m.Rides) },
  { path: 'statistics', loadComponent: () => import('./features/statistics/statistics').then((m) => m.Statistics) },
  { path: 'rides/:id', loadComponent: () => import('./features/ride-detail/ride-detail').then((m) => m.RideDetail) },
  { path: 'login', loadComponent: () => import('./features/login/login').then((m) => m.Login) },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/admin/admin').then((m) => m.Admin),
  },
  { path: '**', redirectTo: '' },
];
