/** Super Admin login credentials. The backend authenticates by userName. */
export interface LoginRequest {
  userName: string;
  password: string;
}

/**
 * Auth payload returned by the admin-login endpoint. Mirrors the backend
 * `AuthResponse`. `userAccountData` shape is not yet modelled — fill in
 * `UserLoginDto` when its fields are known if you want server-provided
 * identity; navigation currently derives the user from the JWT claims.
 */
export interface AuthResult {
  refreshToken?: string;
  accessToken: string;
  userAccountData?: UserLoginDto;
}

/** Placeholder for the backend `UserLoginDto`. Extend with real fields. */
export interface UserLoginDto {
  [key: string]: unknown;
}

/** The current, in-memory authenticated user. */
export interface CurrentUser {
  displayName: string;
  email: string;
  roles: string[];
}

/** Standard JWT claim shape after base64 decoding the payload segment. */
export interface JwtClaims {
  sub?: string;
  email?: string;
  name?: string;
  /** Standard `exp` claim — seconds since epoch. */
  exp?: number;
  /** Role(s): the .NET default role claim URI or a plain `role`/`roles` key. */
  role?: string | string[];
  roles?: string | string[];
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'?: string | string[];
  [key: string]: unknown;
}
