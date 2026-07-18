import { Injectable } from '@angular/core';
import { CurrentUser, JwtClaims } from '../models/auth.model';

const ACCESS_TOKEN_KEY = 'edvanz.access_token';
const ROLE_CLAIM_URI =
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
const NAME_CLAIM_URI =
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name';

/**
 * Single-responsibility store for the JWT. Owns storage, decoding, and expiry
 * checks so that AuthService, the interceptor, and the guards all read token
 * state through one abstraction rather than touching Web Storage directly.
 *
 * Storage strategy: sessionStorage — the token dies with the browser tab,
 * which is the safer default for an admin portal. Swap to localStorage here
 * (one place) if "remember me" persistence is required.
 */
@Injectable({ providedIn: 'root' })
export class TokenService {
  private readonly store: Storage = sessionStorage;

  setToken(token: string): void {
    this.store.setItem(ACCESS_TOKEN_KEY, token);
  }

  getToken(): string | null {
    return this.store.getItem(ACCESS_TOKEN_KEY);
  }

  clear(): void {
    this.store.removeItem(ACCESS_TOKEN_KEY);
  }

  /** True when a token exists AND has not expired. */
  hasValidToken(): boolean {
    const claims = this.decode();
    if (!claims?.exp) {
      return false;
    }
    return claims.exp * 1000 > Date.now();
  }

  /** Builds the current-user projection from token claims, or null. */
  getCurrentUser(): CurrentUser | null {
    const claims = this.decode();
    if (!claims) {
      return null;
    }
    const nameFromUri = claims[NAME_CLAIM_URI];
    const displayName =
      claims.name ??
      (typeof nameFromUri === 'string' ? nameFromUri : undefined) ??
      claims.email ??
      'Admin';
    return {
      displayName,
      email: claims.email ?? '',
      roles: this.extractRoles(claims),
    };
  }

  hasRole(role: string): boolean {
    const claims = this.decode();
    return claims
      ? this.extractRoles(claims).some(
          (r) => r.toLowerCase() === role.toLowerCase(),
        )
      : false;
  }

  private extractRoles(claims: JwtClaims): string[] {
    const raw = claims.role ?? claims.roles ?? claims[ROLE_CLAIM_URI];
    if (!raw) {
      return [];
    }
    return Array.isArray(raw) ? raw : [raw];
  }

  /** Decodes the JWT payload segment; returns null on any malformation. */
  private decode(): JwtClaims | null {
    const token = this.getToken();
    if (!token) {
      return null;
    }
    try {
      const payload = token.split('.')[1];
      const normalized = payload.replace(/-/g, '+').replace(/_/g, '/');
      const json = decodeURIComponent(
        atob(normalized)
          .split('')
          .map((c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0'))
          .join(''),
      );
      return JSON.parse(json) as JwtClaims;
    } catch {
      return null;
    }
  }
}
