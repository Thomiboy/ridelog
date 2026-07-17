import { TranslocoTestingModule, TranslocoTestingOptions } from '@jsverse/transloco';

const en = {
  app: { title: 'RideLog' },
  nav: {
    dashboard: 'Dashboard',
    rides: 'Rides',
    admin: 'Admin',
    login: 'Log in',
    logout: 'Log out',
  },
  dashboard: { title: 'Dashboard', placeholder: 'Your cycling stats will appear here.' },
  rides: { title: 'Rides', placeholder: 'Your rides will appear here.' },
  rideDetail: { title: 'Ride detail', placeholder: 'Ride details and route map will appear here.' },
  admin: { title: 'Admin', placeholder: 'Sync and upload actions will appear here.' },
  login: {
    title: 'Log in',
    email: 'Email',
    password: 'Password',
    submit: 'Log in',
    error: 'Login failed. Check your credentials.',
  },
};

/** Transloco set up with synchronous English translations for component tests. */
export function translocoTesting(options: TranslocoTestingOptions = {}) {
  return TranslocoTestingModule.forRoot({
    langs: { en },
    translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
    preloadLangs: true,
    ...options,
  });
}
