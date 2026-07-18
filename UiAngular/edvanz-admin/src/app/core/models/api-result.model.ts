/**
 * Mirrors the Edvanz backend response envelope. Every endpoint returns:
 *   { success, code, message, data }
 * This is the ONLY file that models the wrapper — services read the payload
 * through `data` exclusively, so a future envelope change stays contained here.
 */
export interface ApiResult<T> {
  /** True when the operation succeeded. */
  success: boolean;
  /** Machine-readable status/error code (e.g. "successlogin"). */
  code?: string;
  /** Localized, user-facing message (en / ar-EG per Accept-Language). */
  message?: string;
  /** The payload. Absent/ignored on failure. */
  data: T;
  /** Optional field-level validation errors keyed by field name. */
  errors?: Record<string, string[]>;
}
