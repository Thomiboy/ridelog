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
  dashboard: {
    title: 'Dashboard',
    tiles: {
      monthDistance: 'This month',
      monthRides: 'Rides this month',
      yearDistance: 'This year',
      yearRides: 'Rides this year',
      yearElevation: 'Climbing this year',
    },
    charts: { monthlyDistance: 'Monthly distance', speedTrend: 'Average speed trend' },
  },
  rides: {
    title: 'Rides',
    empty: 'No rides yet. Sign in as admin to import history or sync from Polar.',
    next: 'Next',
    prev: 'Previous',
    columns: { date: 'Date', distance: 'Distance', duration: 'Duration', avgSpeed: 'Avg speed', elevation: 'Elevation' },
  },
  rideDetail: {
    back: 'Rides',
    distance: 'Distance',
    duration: 'Duration',
    avgSpeed: 'Avg speed',
    maxSpeed: 'Max speed',
    avgHr: 'Avg HR',
    maxHr: 'Max HR',
    elevation: 'Elevation',
    cadence: 'Cadence',
  },
  admin: {
    title: 'Admin',
    error: 'Something went wrong. Please try again.',
    polar: {
      title: 'Polar',
      hint: 'Connect your Polar account.',
      connect: 'Connect Polar',
      connected: 'Connected',
      notConnected: 'Not connected',
      justLinked: 'Polar account linked successfully.',
    },
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
      lastSync: 'Last sync:',
      never: 'never',
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
