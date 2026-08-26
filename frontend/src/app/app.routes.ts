import { Routes } from '@angular/router';
import { adminGuard } from './core/auth/admin.guard';
import { signedInGuard } from './core/auth/signed-in.guard';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard) },
  { path: 'rides', loadComponent: () => import('./features/rides/rides').then((m) => m.Rides) },
  { path: 'activities', loadComponent: () => import('./features/activities/activities').then((m) => m.Activities) },
  { path: 'statistics', loadComponent: () => import('./features/statistics/statistics').then((m) => m.Statistics) },
  { path: 'rides/:id', loadComponent: () => import('./features/ride-detail/ride-detail').then((m) => m.RideDetail) },
  { path: 'login', loadComponent: () => import('./features/login/login').then((m) => m.Login) },
  // The seeded admin's break-glass way in (#186). Nothing links here, and nothing should: a rider
  // who arrived through a provider has no password. Unadvertised, not protected — the guard is the
  // rate limit on /auth/login, and the README says where this door is.
  {
    path: 'login/password',
    loadComponent: () => import('./features/login/password-login').then((m) => m.PasswordLogin),
  },
  { path: 'contact', loadComponent: () => import('./features/contact/contact').then((m) => m.Contact) },
  {
    path: 'riders',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/riders/riders').then((m) => m.Riders),
  },
  {
    path: 'messages',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/messages/messages').then((m) => m.Messages),
  },
  {
    path: 'account',
    canActivate: [signedInGuard],
    loadComponent: () => import('./features/account/account').then((m) => m.Account),
  },
  { path: '**', redirectTo: '' },
];
