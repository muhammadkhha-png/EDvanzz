import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { map, Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResult } from '../models/api-result.model';
import { AuthResult, CurrentUser, LoginRequest } from '../models/auth.model';
import { TokenService } from './token.service';

/**
 * Owns the authentication lifecycle: exchanging credentials for a JWT,
 * exposing the current user reactively, and clearing state on logout.
 * Route protection is delegated to guards; token storage to TokenService.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly tokenService = inject(TokenService);
  private readonly router = inject(Router);
  private readonly baseUrl = `${environment.apiBaseUrl}/auth`;

  /** Reactive current-user; hydrated from an existing token on startup. */
  private readonly userState = signal<CurrentUser | null>(
    this.tokenService.getCurrentUser(),
  );
  readonly currentUser = this.userState.asReadonly();
  readonly isAuthenticated = computed(() => this.userState() !== null);

  login(credentials: LoginRequest): Observable<CurrentUser> {
    return this.http
      .post<ApiResult<AuthResult>>(`${this.baseUrl}/admin-login`, credentials)
      .pipe(
        tap((raw) => {
          if (!environment.production) {
            console.debug('[AuthService] raw login response:', raw);
          }
        }),
        map((result) => this.extractToken(result)),
        tap((token) => this.tokenService.setToken(token)),
        map(() => {
          const user = this.tokenService.getCurrentUser();
          if (!user) {
            throw new Error(
              'Login succeeded but no user could be derived from the JWT ' +
                '(token stored but claims could not be decoded).',
            );
          }
          this.userState.set(user);
          return user;
        }),
      );
  }

  /**
   * Pulls the JWT out of the admin-login response. Envelope is
   * { success, code, message, data }, token at `data.accessToken`.
   * Logs the raw shape in dev and throws a descriptive error (surfaced as a
   * toast) if no token is present, rather than storing an undefined token.
   */
  private extractToken(result: ApiResult<AuthResult>): string {
    const token = result?.data?.accessToken;
    if (!token) {
      if (!environment.production) {
        console.error(
          '[AuthService] No `accessToken` found at `data.accessToken`. ' +
            'Adjust extractToken() to match the real shape:',
          result,
        );
      }
      throw new Error('Login response did not contain an access token.');
    }
    return token;
  }

  /** Clears session and returns to login. Called on logout and on 401. */
  logout(redirect = true): void {
    this.tokenService.clear();
    this.userState.set(null);
    if (redirect) {
      void this.router.navigate(['/login']);
    }
  }

  /** True only if a token exists and is unexpired. */
  isLoggedIn(): boolean {
    return this.tokenService.hasValidToken();
  }

  hasRole(role: string): boolean {
    return this.tokenService.hasRole(role);
  }
}
