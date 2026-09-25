export const environment = {
  production: true,
  // SWA free tier has no linked backend, so the SPA calls the App Service API directly
  // (cross-origin; the API allows this origin via Cors__AllowedOrigins).
  apiBaseUrl: 'https://ridelog-api-bbg9cqdhcxhfazfv.polandcentral-01.azurewebsites.net',

  // CARTO's basemaps need a key (#193). Written in at build time from the MAP_API_KEY secret by
  // scripts/inject-map-key.mjs; empty here so nothing is committed. A client-side tile key cannot be
  // secret — it ships in the bundle and shows in devtools — so the real protection is the domain
  // restriction set at CARTO, and keeping it out of the repo only buys easy rotation.
  //
  // Empty means no basemap rather than a wrong one: the routes still draw on a blank map, because
  // tiling the provider's "API KEY REQUIRED" notice across the screen is worse than drawing nothing.
  mapApiKey: '',
};
