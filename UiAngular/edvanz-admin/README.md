# Edvanz Admin — Teacher Subscription Management (Angular 17)

Admin portal for a Super Admin to manage teachers, their subscriptions, and
per-teacher module access. Standalone components, lazy loading, strict mode.

## Run

```bash
npm install
npm start        # dev server → http://localhost:4200
npm run build    # production build
```

Set the backend URL in `src/environments/environment*.ts` (`apiBaseUrl`).

## Architecture (Clean Architecture, feature-based)

```
src/app/
  core/            singletons: models (domain), services (infrastructure),
                   guards, interceptors  — no UI here
  shared/          reusable presentational components (spinner, toasts,
                   confirm dialog, empty state, breadcrumb, status page)
  features/        auth, dashboard, teachers (list/form/details + subscription
                   & modules tabs) — presentation layer, no HTTP
  layout/          authenticated shell: sidebar, navbar, main layout
```

Dependency direction: `features/layout → core.services → core.models`.
Components never call `HttpClient`; they depend on a service. Services never
leak the raw `ApiResult<T>` envelope — they `map` to the payload type.

## The API seam (the only place to change when real DTOs arrive)

All HTTP lives in four services under `core/services/`:

| Service                | Endpoints (assumed routes — confirm)                          |
| ---------------------- | ------------------------------------------------------------- |
| `AuthService`          | `POST /auth/login`                                            |
| `TeacherService`       | `GET/POST/PUT/DELETE /teachers`, `/teachers/initialize`, `/teachers/dashboard-summary` |
| `SubscriptionService`  | `/teachers/{id}/subscription[/activate|/extend|/end-date|/cancel]` |
| `ModuleService`        | `GET /modules`, `/teachers/{id}/modules[/grant|/revoke]`, `PUT /teachers/{id}/modules` |

The response envelope is modeled on the backend's `Result<T>` /
`PaginatedResponse<T>` in `core/models/`. If the real contract differs, adjust
`api-result.model.ts` / `paginated-response.model.ts` and the affected service
— the presentation layer stays untouched.

## Assumptions to confirm against the real API

1. **Envelope shape** — every response is `{ isSuccess, value, message, errors }`.
2. **Pagination fields** — `items / pageNumber / pageSize / totalCount / totalPages / hasNextPage / hasPreviousPage`.
3. **JWT** — login returns `accessToken`; role claim is the .NET role URI, `role`, or `roles`.
4. **SubscriptionStatus** — serialized as a **string** (`Active | ExpiringSoon | Expired | Cancelled | Pending`). If it's an integer enum, map it in `SubscriptionService`.
5. **Module identity** — modules are addressed by a stable `code` string.
6. **`remainingDays`** — computed server-side; the UI never recomputes it.
7. **Phone rule** — client validates `^\+?\d{8,15}$`; align with the backend’s rule.
8. **Role name** — `permissionGuard` checks for `SuperAdmin`; rename if the claim differs.

## Security

- `authGuard` (protected routes) · `loginGuard` (keep authed users off /login) · `permissionGuard` (role check).
- `authInterceptor` attaches the Bearer token and drives the global spinner.
- `errorInterceptor` centralizes 401 (logout + redirect), 403 (forbidden page), and toasts backend `message` text (supports en / ar-EG).
- Token stored in `sessionStorage` via `TokenService` (swap to `localStorage` in one place if persistence is wanted).
