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
  rides: {
    title: 'Rides',
    empty: 'No rides yet. Sign in as admin to import history or sync from Polar.',
    next: 'Next',
    prev: 'Previous',
    columns: { date: 'Date', distance: 'Distance', duration: 'Duration', avgSpeed: 'Avg speed', elevation: 'Elevation' },
  },
  rideDetail: { title: 'Ride detail', placeholder: 'Ride details and route map will appear here.' },
  admin: {
    title: 'Admin',
    polar: { title: 'Polar', hint: 'Connect your Polar account.', connect: 'Connect Polar' },
    import: {
      title: 'Import history',
      hint: 'Upload GPX/TCX files.',
      button: 'Import',
      result: 'Imported {{imported}}, skipped {{skipped}}, failed {{failed}}.',
    },
    sync: {
      title: 'Sync',
      hint: 'Pull new rides from Polar now.',
      button: 'Sync now',
      result: 'Imported {{imported}}, skipped {{skipped}}, failed {{failed}}.',
    },
  },
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
