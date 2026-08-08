import { Routes } from '@angular/router';
import { signedInGuard } from './core/auth/signed-in.guard';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard) },
  { path: 'rides', loadComponent: () => import('./features/rides/rides').then((m) => m.Rides) },
  { path: 'activities', loadComponent: () => import('./features/activities/activities').then((m) => m.Activities) },
  { path: 'statistics', loadComponent: () => import('./features/statistics/statistics').then((m) => m.Statistics) },
  { path: 'rides/:id', loadComponent: () => import('./features/ride-detail/ride-detail').then((m) => m.RideDetail) },
  { path: 'login', loadComponent: () => import('./features/login/login').then((m) => m.Login) },
  {
    path: 'admin',
    canActivate: [signedInGuard],
    loadComponent: () => import('./features/admin/admin').then((m) => m.Admin),
  },
  { path: '**', redirectTo: '' },
];
