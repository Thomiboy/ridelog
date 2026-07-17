export const environment = {
  production: true,
  // SWA free tier has no linked backend, so the SPA calls the App Service API directly
  // (cross-origin; the API allows this origin via Cors__AllowedOrigins).
  apiBaseUrl: 'https://ridelog-api-bbg9cqdhcxhfazfv.polandcentral-01.azurewebsites.net',
};
