import {
  HttpErrorResponse,
  HttpInterceptorFn,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../services/auth.service';
import { ToastService } from '../services/toast.service';

/**
 * Extracts the best human-readable message from an error body, handling:
 *  - the backend Result<T> envelope         -> { message }
 *  - ASP.NET ProblemDetails                  -> { title, detail }
 *  - ASP.NET ValidationProblemDetails        -> { errors: { field: string[] } }
 *  - a plain string body
 * Returns undefined if nothing usable is found (caller supplies a fallback).
 */
function resolveMessage(error: HttpErrorResponse): string | undefined {
  const body = error.error;
  if (!body) return undefined;
  if (typeof body === 'string') return body;

  if (typeof body.message === 'string' && body.message) return body.message;

  // ValidationProblemDetails: flatten the first field errors into one line.
  if (body.errors && typeof body.errors === 'object') {
    const messages = Object.values(body.errors as Record<string, string[]>)
      .flat()
      .filter(Boolean);
    if (messages.length) return messages.join(' ');
  }

  if (typeof body.detail === 'string' && body.detail) return body.detail;
  if (typeof body.title === 'string' && body.title) return body.title;
  return undefined;
}

/**
 * Central HTTP error policy. Maps status codes to user-facing behaviour:
 *   401 -> session ended: clear auth + redirect to login
 *   403 -> forbidden page
 *   0/5xx/others -> toast with the best available message
 * Prefers the backend's localized `message` field when present, so Arabic
 * (ar-EG) error text surfaces automatically.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const toast = inject(ToastService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // Dev-only: surface the full backend error body so validation failures
      // (e.g. ASP.NET ValidationProblemDetails) are visible at a glance.
      if (!environment.production) {
        console.error(
          `[HTTP ${error.status}] ${req.method} ${req.url}`,
          error.error,
        );
      }

      const backendMessage = resolveMessage(error);

      switch (error.status) {
        case 0:
          toast.error('Cannot reach the server. Check your connection.');
          break;
        case 401:
          toast.warning('Your session has expired. Please sign in again.');
          auth.logout(true);
          break;
        case 403:
          void router.navigate(['/forbidden']);
          break;
        case 404:
          toast.error(backendMessage ?? 'The requested resource was not found.');
          break;
        case 400:
        case 409:
          toast.error(backendMessage ?? 'The request could not be completed.');
          break;
        default:
          toast.error(backendMessage ?? 'An unexpected error occurred.');
      }
      return throwError(() => error);
    }),
  );
};
