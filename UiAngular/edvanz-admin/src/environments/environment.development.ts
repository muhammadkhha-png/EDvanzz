/**
 * Local development environment.
 * apiBaseUrl is RELATIVE so requests hit the Angular dev server origin
 * (http://localhost:4200) and are forwarded to the backend by proxy.conf.json.
 * This sidesteps CORS entirely in dev. Change the target in proxy.conf.json,
 * not here, if your API listens on a different port.
 */
export const environment = {
  production: false,
  apiBaseUrl: 'https://app-edvanz-api-prod.azurewebsites.net/api',
};
