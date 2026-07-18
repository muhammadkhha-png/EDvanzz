import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Role-based gate. Attach via route data:
 *   { canActivate: [authGuard, permissionGuard], data: { roles: ['SuperAdmin'] } }
 * Authentication is assumed already enforced by authGuard ordering.
 */
export const permissionGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const required = (route.data?.['roles'] as string[] | undefined) ?? [];
  if (required.length === 0 || required.some((r) => auth.hasRole(r))) {
    return true;
  }
  return router.createUrlTree(['/forbidden']);
};
