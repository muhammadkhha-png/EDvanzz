import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Blocks protected routes for unauthenticated users. Redirects to /login,
 * preserving the attempted URL so the login flow can return the user there.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isLoggedIn()) {
    return true;
  }
  auth.logout(false);
  return router.createUrlTree(['/login'], {
    queryParams: { returnUrl: state.url },
  });
};
