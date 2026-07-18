import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize } from 'rxjs';
import { LoadingService } from '../services/loading.service';
import { TokenService } from '../services/token.service';

/**
 * Attaches the Bearer token to every outgoing API request and drives the
 * global loading indicator via a ref-counted start/stop around each call.
 * Requests to non-API origins are passed through untouched.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const tokenService = inject(TokenService);
  const loading = inject(LoadingService);
  const token = tokenService.getToken();

  const authed = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  loading.start();
  return next(authed).pipe(finalize(() => loading.stop()));
};
