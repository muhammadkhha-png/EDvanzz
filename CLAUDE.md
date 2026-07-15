# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# CLAUDE.md — Edvanz Project Brain

> Drop this file in the **repo root** (`/`). Claude Code reads it automatically on every
> invocation. It is the authoritative source of architectural decisions, enforced patterns,
> known bugs, and active work items. Keep it updated as decisions evolve.

---

## 0. Commands

**Build / run (from repo root, solution file is `Edvanz.slnx`):**

```bash
dotnet restore Edvanz.slnx
dotnet build Edvanz.slnx
dotnet run --project Edvanz.API      # Swagger UI at /swagger (Development/Staging)
```

**EF Core migrations** (tool pinned in `Edvanz.API/dotnet-tools.json`, run once per clone):

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project Edvanz.Infrastructure --startup-project Edvanz.API
dotnet ef database update --project Edvanz.Infrastructure --startup-project Edvanz.API
```

`migrate.sql` at repo root is a generated migration script (EF `script` output), not
hand-written seed SQL — don't edit it directly. `.github/workflows/deploy.yml` is the
authoritative CI/CD sequence (restore → build → idempotent EF script applied to Azure SQL →
self-contained publish → `az webapp deploy --type zip` via OIDC) if you need to see how
changes reach production.

**Deploy behavior (2026-07-16, rev 3):** push to `master_integration` → CI now runs two
migration gates before touching Azure: (1) `dotnet ef migrations has-pending-model-changes`
rejects commits whose model isn't covered by a migration; (2) the generated `migrate.sql`
is rehearsed twice (fresh apply + idempotent re-run) against a throwaway SQL Server 2022
container with `sqlcmd -b`. The prod `azure/sql-action` step also runs with `-b`, so any
SQL error aborts the job BEFORE `az webapp deploy` — code and schema can no longer diverge
(closes the BUG-10 delivery hole). Gates add ~1.5–2 min. Rev 2 behavior (unchanged below):
push → ~2–2.5 min CI →
**async** zip deploy + explicit verification: the workflow polls the deployment record to
`deployed+active` (cap 2 min) and then `/health/live` (anonymous, cap 5 min). Healthy path
≈ 3.5–4.5 min total. An App Service quirk can start the first replacement container with a
**stale app-settings snapshot** (e.g., missing/pre-rotation `ConnectionStrings__con` → SQL
18456); the app fails fast by design and Azure auto-replaces the container over ~8–10 min.
When that happens the run ends GREEN with a `::warning::` ("site not healthy within 5 min")
instead of blocking 10 minutes — the swap still completes on Azure's side. Since rev 2 a
RED run means a REAL failure (record status 3, or record never active). The old synchronous
step used to sit blind for 10 min and report false "site failed to start" failures. `appsettings.json` holds a design-time placeholder connection string only;
runtime configuration comes from App Service settings (`ConnectionStrings__con`), and
Program.cs refuses to boot on the placeholder. `WEBSITE_RUN_FROM_PACKAGE` is inert on this
Linux plan and the workflow removes it if it reappears.

**Tests:** there is no automated test project in this solution. Validate changes with
`dotnet build` plus manual exercise via Swagger UI or the root `EDvanz.postman_collection.json`
(Postman collection) — that collection is the de facto test suite until one is added.

**WhatsApp microservice** (`whatsapp-service/`): a standalone Node/Express process
(`whatsapp-web.js` + Puppeteer) the API calls over HTTP via `IWhatsAppSender`
(`AddHttpClient<IWhatsAppSender, WhatsAppSender>()` in `Program.cs`). It is **not** part of
the .NET solution/build — run it separately:

```bash
cd whatsapp-service
npm install
node index.js   # first run prints a QR code to link WhatsApp Web
```

---

## 1. Solution Structure

```
Edvanz.sln
├── Edvanz.Domain/          # Entities, Interfaces, Enums, Constants, Value Objects
├── Edvanz.Application/     # Services, DTOs, Service-contract interfaces, Result pattern
├── Edvanz.Infrastructure/  # EF Core DbContext, Repos, Background Jobs, External integrations
└── Edvanz.API/             # Controllers, Middleware, Program.cs, appsettings.json
```

**Dependency rule (strictly enforced):**
`Domain ← Application ← Infrastructure ← API`
Never reference Infrastructure from Domain or Application. Never reference API from any
other project.

---

## 2. Technology Stack

| Concern | Technology |
|---|---|
| Runtime | .NET 10 / ASP.NET Core |
| ORM | EF Core 10 (Code-First, SQL Server) |
| Database | SQL Server (MSSQL 2025 on MonsterASP.NET) |
| Auth | JWT Bearer + custom SecurityStamp validation middleware |
| Cache | Redis via Upstash (production) / Memurai port 6379 (local dev) |
| Background jobs | Hangfire with SQL Server storage |
| PDF export | QuestPDF (Community license, Noto Sans Arabic for RTL) |
| Localization | `IStringLocalizer`, `Messages_en.resx` + `Messages_ar.resx` (Egyptian Arabic) |
| Hosting | MonsterASP.NET free tier (IIS/Windows Server, Let's Encrypt HTTPS) |

### 2.1 Program.cs Wiring Points

Cross-cutting registrations live in `Edvanz.API/Program.cs`, not auto-discovered — check here
before assuming something is missing:

- Recurring Hangfire jobs are registered inline via `RecurringJob.AddOrUpdate<T>(...)`:
  subscription reminder dispatcher (09:00 Africa/Cairo), pending-payment expiry sweep
  (hourly), assistant cleanup (01:00 Africa/Cairo), recurring-assignment materializer
  (06:00 Africa/Cairo). A new recurring job needs its own registration here.
- Swagger example providers (see `IEndpointExampleProvider`, §3.7-adjacent Swagger tooling)
  are added one `AddSingleton<IEndpointExampleProvider, ...>()` call at a time.
- `/health/live` (process-up only) and `/health/ready` (checks SQL Server + Hangfire) are
  the health-check endpoints; `/hangfire` is the dashboard, gated to the `SuperAdmin` role.

---

## 3. Core Patterns — Follow These Without Exception

### 3.1 Repository + Unit of Work

- All data access goes through `IUnitOfWork` → named repo properties or
  `GetRepository<T, TKey>()` for generic CRUD.
- **Named repo methods encapsulate all query logic.** No raw LINQ expression predicates
  in the service layer. No `GetQueryable()` calls from services.
- Extended repos (e.g., `IUserRepo`, `IAttendanceRepo`) inherit `IGenericRepo<T, long>`
  and add named methods for every domain-specific query.

```csharp
// CORRECT
var teacher = await _unitOfWork.Users.GetTeacherByUserIdAsync(userId);

// WRONG — raw predicate in service layer
var teacher = await _unitOfWork.GetRepository<Teacher, long>()
    .FindAsync(t => t.UserId == userId);
```

### 3.2 Result Pattern

All service methods return `Result<T>`. Controllers call `ToResponse(result)` from
`ApiBaseController`. Never throw exceptions for business-logic failures.

```csharp
// Service
return Result<MyDto>.Failure(_localizer, "SomeMessageKey");
return Result<MyDto>.Success(dto, _localizer, "Success");

// Controller
return ToResponse(await _service.DoSomethingAsync(...));
```

### 3.3 `teacherId` Must Come from JWT — Never from Route/Body

Sourcing tenant identity from request parameters is an **IDOR vulnerability**.
Use `ResolveTeacherIdAsync()` from `ModuleSixApiBaseController` (or the equivalent
pattern for other base controllers).

```csharp
// CORRECT
var teacherId = await ResolveTeacherIdAsync();
if (teacherId is null) return TeacherNotResolved();

// WRONG — IDOR
long teacherId = request.TeacherId;
```

### 3.4 Localization

All user-facing strings use `IStringLocalizer`. Keys are defined in
`Messages_en.resx` and `Messages_ar.resx`. Egyptian Arabic dialect in the AR file.

### 3.5 Async/Await Everywhere

All I/O operations are `async Task`. No `.Result` or `.Wait()` anywhere.

### 3.6 Constructor Injection Only

No service-locator pattern, no `IServiceProvider` lookups in business code.

### 3.7 XML Documentation

All public interfaces, methods, and non-obvious logic blocks carry `/// <summary>` XML
doc comments. Reference requirement IDs where applicable, e.g. `/// REQ-ATT-007`.

### 3.8 Pagination

List endpoints return `PaginatedResponse<T>`. Page size and page number come from
query parameters. Total count is calculated separately before fetching the page.

---

## 4. EF Core Rules — Read Before Touching OnModelCreating

### 4.1 Fluent API is the SOLE source of truth for FK behavior

**Never mix `[ForeignKey]` data annotations with Fluent API `OnDelete` configuration
on the same relationship.** EF Core 10 silently drops the explicit `OnDelete` behavior
when both exist on the same FK. This caused the NoAction-everywhere bug in an early
migration.

```csharp
// CORRECT — Fluent API only
entity.HasOne(v => v.Teacher)
    .WithMany()
    .HasForeignKey(v => v.TeacherId)
    .OnDelete(DeleteBehavior.NoAction);

// WRONG — annotation coexists with Fluent; OnDelete gets silently dropped
[ForeignKey(nameof(Teacher))]
public long TeacherId { get; set; }
// + HasForeignKey(...).OnDelete(DeleteBehavior.NoAction) in OnModelCreating
```

Use `[ForeignKey]` annotations only when there is NO Fluent API `OnDelete` configuration
for that FK. When in doubt, configure entirely in Fluent API.

### 4.2 Delete Behaviors

| Default policy | Exceptions |
|---|---|
| `NoAction` (app-layer cascade) | Explicitly noted per entity |
| Soft-delete via `IsDeleted`/`DeletedAt` for most entities | Hard-delete for `Session` and `VideoAsset` (see below) |

### 4.3 Hard-Delete vs Soft-Delete

- **Soft-delete** (`IsDeleted` + `DeletedAt`): default for all entities.
- **Hard-delete exceptions**: `Session`, `SessionGroup` (documented), `VideoAsset`
  (audit snapshot captured in `VideoAssetAudit` atomically before deletion).

### 4.4 Composite FKs for Tenant Integrity

When an entity stores `TeacherId` as a denormalized tenant column AND references a
parent that also has `TeacherId`, use a composite FK `(EntityId, TeacherId)` →
`(Parent.Id, Parent.TeacherId)` to enforce tenant integrity at the DB level.
See `VideoScope` → `VideoAsset` for the reference implementation.

---

## 5. Authentication & Security

### 5.1 SecurityStamp Invalidation

The stamp bump **must** run inside the same transaction as the triggering write
(password change, deactivation, permission revoke) via `_authInvalidation.InvalidateUserAsync(userId)`.
It **must be called before** `SaveChangesAsync` so the bump joins the transaction.

Post-commit side effects (activity logging, audit) run **after** `CommitAsync` as
best-effort, wrapped in their own `try/catch`.

```csharp
// CORRECT ordering
await _authInvalidation.InvalidateUserAsync(userId); // stamp bump in tx
await _unitOfWork.SaveChangesAsync();
await _unitOfWork.CommitAsync();
// activity log here, outside tx
```

### 5.2 Service-Layer Commit Ownership

Internal helpers (e.g., `RecordLoginActivityAsync`) must **not** call their own
`SaveChangesAsync`. The caller owns the commit boundary.

### 5.3 FallbackPolicy vs DefaultPolicy

`[Authorize]` on a controller class suppresses the global `FallbackPolicy`
(which carries `ActiveSubscriptionRequirement`). To enforce subscription checks
universally, the requirement must live in `DefaultPolicy`, not only in
`FallbackPolicy`.

### 5.4 JWT Claims

JWT carries only: `NameIdentifier` (user id), `Name` (username), `Role`, `SecurityStamp`.
Permission and module claims were removed (v1→v2 architectural fix — they were stale).
Live permissions are resolved from `UserAuthSnapshot` on every request by
`SecurityStampValidationMiddleware`.

---

## 6. Hangfire & Background Jobs

### 6.1 Queue Names

| Queue | Purpose |
|---|---|
| `"notifications"` | Subscription reminders, renewal confirmations, payment rejections |
| `"assignment-materialization"` | Recurring assignment occurrence generation |

Queue name is declared on the **interface method** via `[Queue("...")]`, not on the
implementation class.

### 6.2 Architecture Constraint

The Application layer **must not** reference `IBackgroundJobClient` directly.
Background job enqueuing belongs in Infrastructure dispatcher jobs
(e.g., `SubscriptionReminderDispatcherJob`, `RecurringAssignmentDispatcherJob`).
Any existing `IBackgroundJobClient` usage in Application is a **known architectural
violation** flagged for remediation.

### 6.3 Intent-Based Interfaces

Prefer named job interfaces (`IRenewalNotificationJob`, `ISubscriptionReminderJob`)
over generic scheduler wrappers. The interface is the contract; Hangfire's job
activator instantiates through DI.

### 6.4 Idempotency

Every job implementation must be idempotent. Hangfire retries 3 times with
exponential backoff. Throw on failure so Hangfire records and retries.

---

## 7. Module Status & Active Work Items

### 7.1 Module 8 — Messaging (P0 defects outstanding)

| ID | Defect | Status |
|---|---|---|
| P0-A | Auth endpoints commented out on `MessageController` | Open |
| P0-B | `EncryptedCredentials` field never populated (latent bug) | Open |
| P0-C | Wrong phone field guard in send path | Open |
| P0-D | `MessageSenderJob` duplicate log rows on retry | Fix designed — `CreatePendingAsync`/`MarkResultAsync` on `IMessageLogService`, `MessageLogId` threaded through `MessageSendPayload` |

**P1 backlog**: log row correctness, resend restriction, preview/confirm split (REQ-MSG-028).

### 7.2 Attendance Module

- Full auth/IDOR remediation complete (Phases 1–3).
- `ParentAttendanceController` implemented.
- **Open**: `AttendanceRepo.UpdateAbsenceCounterAsync` — same guard fix needed as was
  applied to `PaymentRepo.UpdatePaymentCounterAsync` (forcing `EntityState.Modified`
  on a freshly-`Added` entity is a bug).

**Student/parent-facing month view — occurrence-overlay contract (shipped 2026-07-13,
commit `5bae703`).** `GET /api/attendance/student/teachers/{teacherId}/month?year=&month=`
(mirror: `ParentAttendanceController`; the teacher `AttendanceController` month route shares
the same service method `GetStudentTimelineMonthAsync`). The calendar is driven by the
session's **scheduled `SessionOccurrence`s**, NOT merely by `AttendanceRecord` rows — so the
student sees every class day, including upcoming and not-yet-marked ones. Rules:

- **Two gates, unchanged**: JWT → `StudentUser` → **Active** `StudentTeacherLink` → bound
  `TeacherStudentId` (403 if unlinked/unbound; `ResolveStudentForTeacherAsync`, replicated in
  `StudentAttendanceController`/`StudentVideosController`), THEN
  `IsAttendanceVisibleTo(config, viewer)` on `StudentVisibilityAttendance` /
  `ParentVisibilityAttendance` (fail-closed on null config).
- **`year`/`month` optional** on `StudentTimelineMonthRequest` (now `int?`) → default to the
  teacher's local (Africa/Cairo) current month via `ITimeZoneService`, matching the payment
  module's month scoping.
- **Occurrences clipped to the enrollment window**: for each `StudentSessionAssignment`
  overlapping the month, pull `GetOccurrencesBySessionAndDateRangeAsync` bounded by
  `AssignedAt`/`UnassignedAt` (BR-ATT-001 — no obligation before/after enrollment). Records are
  overlaid by `SessionOccurrenceId`; a record with no matching cell (cross-session present, or
  outside the window) is surfaced as its own date-driven cell so nothing marked is lost.
- **`MonthlyAttendanceSummaryDto`** carries top-level `SessionId`/`SessionName` (screen header,
  from the active/most-recent assignment, else latest record), `TotalOccurrences` (= scheduled
  days = `Days.Count`), `MarkedOccurrences`, `TotalPresent` (Present + CrossSessionPresent),
  `TotalAbsences`, `AttendancePercentage`, a `Days[]` calendar (`StudentAttendanceDayDto`:
  `Date`, `SessionOccurrenceId`, `SessionId`, `SessionName`, **nullable `Status`** where
  `null` = scheduled/unmarked, `IsPast`), and the original `Records[]` (kept for back-compat;
  the export path builds this DTO inline and is untouched).
- **Percentage denominator = Present + Absent only** — `Held` and unmarked/upcoming days are
  excluded. `Status` serializes as a **string** (global `JsonStringEnumConverter`).
- Additive, **no migration**. Apply this same occurrence-overlay shape to the not-yet-built
  student payment/exam/homework calendar views (see §7.2b).

### 7.2b Student User Module — Request/Approval Linking (redesigned 2026-07-12; Connection↔Link split 2026-07-13)

The original AAM-FR-05.5 3-credential instant link (TeacherCode + StudentCode +
HashedToken) was **replaced** by a request/approval flow. Do not reintroduce the
credential flow on the student side (the PARENT Method B flow still uses it —
`ParentUserService.LinkTeacherToChildAsync` — until that module is migrated).

- **Lifecycle** (`LinkStatus`): `Pending`(3) → `Active`(1) accept / `Rejected`(4)
  reject / `CancelledByStudent`(6); `Active` → `Unlinked`(2) student removes /
  `RemovedByTeacher`(5). Terminal rows are kept for audit; a **filtered unique
  index** (`[LinkStatus] IN (1,3)`) allows one live row per (StudentUserId,
  TeacherId) and unlimited history — keep the filter literals in sync with the enum.
- **Student side** (`StudentUserController`, `api/studentuser/me/*`): identity is
  ALWAYS resolved JWT → `GetActiveStudentUserByUserIdAsync` (the old route-id
  endpoints were IDOR-prone and were removed). `POST me/link-requests`
  {teacherCode, studentName, studentCode?} creates the Pending row;
  `GET me/teachers` returns the latest row per teacher with `status` so the
  student sees accepted/rejected outcomes; `DELETE me/teachers/{teacherId}`
  cancels a Pending or unlinks an Active row.
- **Teacher side** (`TeacherStudentLinksController`, `api/teacher/student-links`):
  `GET my-code` (shareable 8-digit code), `GET requests` (inbox + suggested
  roster match from the typed student code), `POST requests/{id}/accept`,
  `POST requests/{id}/reject`, `GET` (linked students), `POST remove` (bulk),
  `POST {linkId}/bind` + `POST {linkId}/unbind` (link management — see next bullet).
  One roster record ↔ one student account, enforced BOTH app-level (accept + bind
  re-run `IsTeacherStudentActivelyLinkedAsync`) AND by the filtered unique index
  `UX_StudentTeacherLinks_TeacherStudentId_Active`
  (`[LinkStatus]=1 AND [TeacherStudentId] IS NOT NULL`); the redesign migration
  self-heals legacy duplicate claims (keeps the newest Active row, demotes older
  ones to Unlinked) before creating it.
- **Connection vs Link — SEPARATE axes (shipped 2026-07-13, commit `5d00efd`).**
  Accepting only CONNECTS the account (`LinkStatus.Active`); binding it to a
  `TeacherStudent` (roster) record is a distinct, re-pointable step. `accept` with a
  `teacherStudentId` links atomically ("Accept & link"); WITHOUT one it accepts
  UNBOUND (`TeacherStudentId = null` — Active but **Not linked**: connected, sees
  NOTHING, since every module joins through that FK). `POST {linkId}/bind`
  {`teacherStudentId?` | `studentCode?`} links or re-points ("Change linked student");
  `POST {linkId}/unbind` clears the binding yet stays Active. Both are `Student/Edit`,
  return the updated `LinkedStudentListItemDto`, and re-run the one-account-per-record
  guard. `IsLinked` (= `Active && TeacherStudentId != null`) is exposed on the teacher
  `LinkedStudentListItemDto` AND the student `StudentDashboardTeacherDto` — distinct
  from `Status`; do NOT add a `LinkStatus` member for it (the filtered-index literals
  `[LinkStatus] IN (1,3)` are hand-synced). No migration (`TeacherStudentId` already
  nullable). `IStudentLinkNotifier.NotifyLinkBindingChangedAsync(linked)` fires on
  bind/unbind. Accept no longer auto-matches by the typed code — the client passes
  `suggestedMatch.teacherStudentId` for one-tap "Accept & link".
- **End-of-link audit**: `RespondedByUserId` records who accepted/rejected;
  `RemovedByUserId` records who ENDED the link (student on Unlinked/
  CancelledByStudent, teacher/assistant on RemovedByTeacher). Plain columns, no FK.
- **Visibility concept unchanged**: `TeacherConfiguration.StudentVisibility*`
  flags are still returned per dashboard entry and still gate the per-module
  student endpoints (attendance/videos today; payments/exams/homework when built).
- **Notifications**: `IStudentLinkNotifier` (inbox `UserNotification` +
  FCM push, localized to the RECIPIENT's language) fires post-commit,
  best-effort, on request-received / accepted / rejected / removed-by-teacher.
- Login `teacherIds` now come from Active links joined through
  `StudentUsers.UserId` (`StudentTeacherLinkRepo` — was comparing User.Id to
  StudentUser.Id and ignoring status).

**OPEN WORK ITEM — Parent Method B migration (deferred; still POSTPONED as of
2026-07-13):** `ParentUserService.LinkTeacherToChildAsync` still uses the OLD
3-credential flow (TeacherCode + StudentCode + HashedToken via `LinkTeacherToChildDto`
and `GetTeacherStudentByLinkingCredentialsAsync`). The PARENT side must EVENTUALLY get
the **same treatment as the student side above — request/approval AND the
Connection-vs-Link split**: parent sends a request for a child → teacher accepts,
CONNECTING it → teacher separately binds/unbinds a roster record; a
`ParentChildTeacherLink` lifecycle mirrors `LinkStatus` plus an `IsLinked` flag and
`bind`/`unbind` endpoints. **Deliberately postponed — not scheduled.** Until then: do
NOT remove `TeacherStudent.HashedToken`, the credentials repo method, or the
`StudentCodeRequired`/`HashedTokenRequired`/`InvalidLinkCredentials` resx keys — they
are all load-bearing for parents. Reference spec: this section +
`docs/student-linking-openapi.json`.

### 7.3 DbInitializer Refactor

Proposed split into five partial files. **Scope decision pending** — confirm before
proceeding.

### 7.4 Payment Module — Business Logic (authoritative)

The payment screens (`PaymentScreenService` over the `api/v1/*` routes, plus
`PaymentController`/`PaymentService` for `api/Payment/*`) follow these rules. The
API response shapes are fixed (frontend contract) — change logic, never payloads.

**Periods & the "selected month".** On assignment, one `PaymentPeriod` per month is
generated from the join month to the session end (monthly sessions), so **future
months exist as `Unpaid` rows up front**. Therefore every paid/unpaid/prorated/
outstanding computation must be judged **only through the month the screen is looking
at** — filter `PeriodStart <= selectedMonthEnd`. Never derive buckets/arrears from the
all-time `StudentPaymentCounter` (it counts future months). Repo helpers:
`GetUnpaidPeriodsThroughAsync`, `GetOverdueTotalThroughAsync`. Screens with no explicit
month use the teacher's **current local (Africa/Cairo) month** via `ITimeZoneService`.

**Status buckets (through the selected month).** `Paid` = caught up through that month;
`Unpaid` = any unpaid month ≤ that month; `Partial` = that month partly paid;
`Prorated` = the prorated first month **only when the teacher has proration enabled**.

**Buckets are assigned-only and reconcile to `TotalStudents`.** The status headcounts
(`statusBreakdown.paid/prorated/unpaid` on `/api/v1/payments/tracking`) and the per-status
lists (`/api/v1/payments/students?status=…`) classify **only students currently assigned to
a session** (`TeacherStudents.SessionId != null` — the same population as
`CountAssignedStudentsAsync`/`summary.totalStudents`). So `paid + prorated + unpaid ==
totalStudents` by construction: an assigned student with no outstanding period through the
selected month (caught up, or no obligation generated yet) counts as `Paid`. **Do not** count
students off their historical `PaymentPeriods` alone — formerly-assigned students keep old
periods and would inflate the buckets past the assigned headcount (this was the paid+unpaid >
total bug). Both `GetStudentPaymentStatusCountsAsync` and `GetStudentsByPaymentStatusPagedAsync`
gate on the assigned-student id set. Trade-off: unassigned students with lingering arrears are
excluded from these headcounts/lists (their money still shows in cash/expected aggregates). Edge
case: the `paid` count may exceed the `status=paid` list by 1 per assigned student who has no
period row at all (rare — assignment normally generates one).

**Collection engine (`CollectPaymentAsync`, monthly sessions).** A payment fills the
**oldest unpaid month first and cascades forward** across months; each cleared month is
attributed to its own period, while **one** `PaymentTransaction` records the whole cash
event (dated now). Advance is capped at **current month + 1** (end of next month); cash
beyond that is rejected with `PaymentAmountExceedsAdvanceLimit` (422). Partial payments
allowed (a short month stays `PartiallyPaid`). The counter advances
`TotalPaidPeriods`/`TotalUnpaidPeriods` by the number of months a single collection fully
cleared. **Per-session (per-class) billing keeps its original single-period behavior** —
the monthly rules apply to `PaymentType.Monthly` only. `mark-paid` and the collect
`lookup.AmountDue` use the **server-computed total arrears through the current month**,
not a single month or a client-supplied amount.

**Dashboards = actual cash.** Dashboard/wallet "collected this month" is **actual cash
physically collected this calendar month** (by transaction date, net of refunds via the
`!IsDeleted` filter), so it can exceed "expected" when students pay arrears or one month
ahead. "Expected" stays period-based (assigned students × their monthly amount). Repo:
`GetCashCollectedInRangeAsync`. The shared `GetDashboardAggregatesAsync` is period-based
and reused by reports — do not switch it to cash; add a dedicated method instead. All
per-session dashboard figures are month-scoped (`GetSessionMonthCollectionAsync`,
`GetAssignedStudentCountsPerSessionAsync`); `TotalStudents` = students assigned to a
session (`CountAssignedStudentsAsync`).

**Assistant wallet & refunds.** Wallet rises on collect, falls on refund; a **refund**
(a `Deleted`/`Reversed` `PaymentEditLog` on a transaction) is deducted from the
**original collector** and shown in that collector's **month log as a negative-amount
entry** in the same list as collections (`GetCollectorTransactionsInRangeAsync` +
`GetCollectorRefundsInRangeAsync`, merged). `TotalCashCollected` = money in − money out
for the month. **Withdraw** = tutor taking cash (a `WalletResetLog`), reduces
`CurrentBalance` only, distinct from a refund.

**Transfer between sessions.** No proration on monthly→monthly; the carried balance is
the source session's **arrears through the current month** (`GetOverdueTotalThroughAsync`,
not the all-time counter), written as one `IsCarriedForward` period in the destination.

**Purge.** Permanently deleting a student **deletes** its `PaymentPeriods`
(`OnStudentPermanentlyDeletedAsync`) — do not orphan them (nulled periods leak their
`AmountDue` into aggregates). The `PaymentTransaction→PaymentPeriod` FK is
`ON DELETE SET NULL`, so audit transactions survive with denormalized data. Dashboard
aggregates also defensively exclude `TeacherStudentId == null` periods.

**Aggregates exclude orphans.** Any period-summing query that feeds a total must ignore
`TeacherStudentId == null` rows.

---

## 8. Known Bugs (Fixed — Do Not Reintroduce)

| Bug | Location | Fix |
|---|---|---|
| BUG-1 | `UnitOfWork.CommitAsync` | `_transaction` was not nulled after commit; `HasActiveTransaction` stayed `true`. Fixed. |
| BUG-2 | `GenericRepo.UpdateAsync` / `DeleteAsync` | EF Core `Entry().State` and `Remove()` are synchronous; async signature kept for convention but `await Task.CompletedTask` added. |
| BUG-3 | `PaymentRepo.UpdatePaymentCounterAsync` | Was forcing `EntityState.Modified` on a freshly-`Added` entity. Fixed with state guard. |
| BUG-4 | EF FK/annotation coexistence | `[ForeignKey]` + Fluent `OnDelete` silently drops `OnDelete`. Fluent API is sole authority. |
| BUG-5 | Payment buckets counted future months | Paid/unpaid/outstanding derived from all periods (incl. pre-generated future months) → everyone read as unpaid. Now judged through the selected month. See §7.4. |
| BUG-6 | Purge orphaned payment periods | Permanent student delete nulled `PaymentPeriods.TeacherStudentId`, leaving orphans that leaked `AmountDue` into dashboards. Now periods are deleted on purge; aggregates also exclude null-student rows. See §7.4. |
| BUG-7 | `AttendanceRepo.GetPagedAttendanceStudentListAsync` | `occurrenceId.HasValue ? subquery : null` in a projection put an **untyped NULL constant** in the SQL tree; EF throws `Expression 'NULL' in the SQL tree does not have a type mapping assigned` at query-compile time for any date with no `SessionOccurrences` row → data-independent 500 (the 2026-07-12 attendance student-list outage; PR #8 was innocent). The occurrence guard now lives INSIDE the subquery `Where`, so the member is always a typed subquery. Never project a bare `null` branch against a subquery. |
| BUG-8 | Same method — purge ghosts | Purging a student SET-NULLs `StudentSessionAssignments.TeacherStudentId` but leaves `IsActive=1`; the row surfaced as an "Unknown" student and crashed the `TeacherStudentId!.Value` mapping. Assignment queries that materialize students must filter `Where(a => a.TeacherStudent != null)` (also hides soft-deleted students via the global filter). |
| BUG-9 | Duplicate migration lineages | The `video phase 01` merge re-added the old init migration chain alongside the 07-09 baseline chain; two baseline-chain migrations duplicated index creations (`Add_TeacherStudent_Phone_UniqueIndexes`, `Add_PP_Status_PeriodStart_Index_Catchup`), so `dotnet ef database update` on a FRESH database failed (error 1913). Duplicates deleted 2026-07-12; prod unaffected (their ids remain in `__EFMigrationsHistory`; EF ignores orphan history rows). Keep a single lineage and verify a fresh-DB build after any merge touching `Migrations/`. |
| BUG-11 | `20260708193718`/`20260708220307` phone-index migrations never applied on prod | The first created a GLOBAL unique `ParentPhoneNumber` index that included soft-deleted rows → failed on an existing duplicate; the second then failed dropping the index the first never created. Both re-ran and re-failed silently on every deploy (no `-b`, see CI note below), so prod had NO phone uniqueness (duplicate-phone protection relies on the DB index via `ResolveUniqueViolationKey`) and was missing `IX_PP_TeacherId_Status_PeriodStart`. Fixed 2026-07-16: both files deleted (BUG-9 precedent) and `20260715231605_RepairTeacherStudentPhoneIndexes` defensively converges all environments — parent phone index recreated NON-unique (siblings share it — see the uniqueness rule below), student phone unique filtered, active student-phone duplicates cleaned (earliest row keeps the phone), PP index caught up. |
| BUG-10 | `20260715202558_AddSessionOccurrenceSlotKeys` backfill referenced same-batch new columns | The migration did `AddColumn DayPositionIndex/WeekStartDate` then a bare `migrationBuilder.Sql` **UPDATE** setting those columns. EF emits a migration's ops as ONE `GO`-less batch, and the idempotent deploy script keeps them in one batch, so SQL Server bound the UPDATE at batch-compile time when the columns didn't exist yet → **error 207 "Invalid column name"** (under the idempotent IF-wrapper it surfaced as a downstream **1505** duplicate-key: the un-backfilled default `(2000-01-01,1)` rows collide on the new unique index). The migration silently failed to apply (see the CI note below), yet the code that queries the columns shipped → prod 500'd on every attendance call (the 2026-07-15/16 outage). **Fix:** wrap the backfill UPDATE in `EXEC(N'...')` so name resolution is deferred to run time, after the columns exist. Applied to prod out-of-band and recorded in `__EFMigrationsHistory`. Never backfill a just-added column with a bare `Sql()` UPDATE in the same migration — use `EXEC()` (or a separate migration). |

**CI migration delivery — root cause of the 2026-07-15/16 attendance outage (deploy.yml `Apply EF migrations`) — RESOLVED 2026-07-16.** `azure/sql-action@v2` used to run the multi-batch idempotent `migrate.sql` (one `BEGIN TRAN…COMMIT` per migration) via go-sqlcmd **without `-b`**, so when a migration's batch errored, its own transaction rolled back (migration NOT recorded) but the runner **continued to the next migration and still exited 0** — a broken migration was silently skipped while the code that needed it deployed anyway. This is why BUG-10 shipped, and it also silently skipped the `20260708193718`/`20260708220307` phone-index migrations on every deploy since 2026-07-08 (see BUG-11). Fixed by: (a) BUG-11's repair migration clearing the failing backlog, (b) `arguments: '-b'` on the sql-action step (any SQL error → non-zero exit → job fails BEFORE `az webapp deploy`), and (c) the two pre-Azure migration gates described in §0 (model-coverage check + fresh-DB rehearsal of `migrate.sql`). Do not remove `-b` or the gates.

**Phone-number uniqueness rule (decided 2026-07-16):** `ParentPhoneNumber` is **NOT unique** — one parent legitimately has several children on the same roster, so it carries only a non-unique lookup index `IX_TeacherStudents_TeacherId_ParentPhoneNumber`. `StudentPhoneNumber` IS unique per teacher among active rows (filtered `IS NOT NULL AND IsDeleted = 0`). Bulk import dedupes student phones (not parent phones) within a batch; `ResolveUniqueViolationKey` maps only StudentPhoneNumber/StudentCode violations.

---

## 9. Seeding Rules

- **Service-driven seeding only** (all data flows through Application layer services).
- Never fabricate transactional rows (attendance records, payment counters, session
  occurrences) directly via repo in seeders — produces referentially inconsistent state.
- `DbInitializer` is the entry point. Follow Option A (service-driven) established pattern.

---

## 10. Naming & Code Conventions

- **PascalCase** for all C# identifiers (classes, methods, properties).
- **camelCase** for `PaginatedResponse` fields (existing convention).
- **`ResolveTeacherIdAsync()`** — canonical method name for JWT→teacher resolution in
  base controllers.
- **`ToResponse(result)`** — canonical method name for Result→IActionResult in
  `ApiBaseController`.
- Module-scoped base controllers (e.g., `ModuleSixApiBaseController`) for shared JWT
  resolution logic. Do not copy-paste the resolution logic into individual controllers.
- Required-on-the-wire DTO fields use `[Required]` so ASP.NET model binding surfaces
  400s with field names automatically.
- Enum-valued query parameters use `JsonStringEnumConverter` so Swagger and clients
  see string values, not integers.
- `[Timestamp]` on `RowVersion` properties for optimistic concurrency.

---

## 11. Requirement IDs

The authoritative spec is `Edvanz_Requirements.pdf` (attached to the Claude.ai project).
Reference IDs follow this pattern:

- `REQ-XXX-NNN` — Functional requirements
- `BR-XXX-NNN` — Business rules
- `AAM-FR-XX-NN` — Assistant Access Management requirements

Always cross-reference code comments to the relevant `REQ-*` / `BR-*` IDs.

---

## 12. What NOT to Do

| Anti-pattern | Reason |
|---|---|
| Raw LINQ predicates in service layer | Bypasses named-method contract; leaks query logic |
| `GetQueryable()` calls from services | Same violation as above |
| `[ForeignKey]` annotation + Fluent `OnDelete` on same FK | Silently drops `OnDelete` in EF Core 10 |
| `teacherId` from route or body | IDOR vulnerability |
| `SaveChangesAsync` inside internal helper methods | Caller owns commit boundary |
| `IBackgroundJobClient` in Application layer services | Hangfire type belongs in Infrastructure |
| Security stamp bump after `SaveChangesAsync` | Stamp must join the transaction |
| Generic `IJobScheduler` wrappers that re-export Hangfire | Use intent-based interfaces instead |
| Fabricating transactional rows directly in seeders | Referentially inconsistent state |
| Throwing exceptions for business-logic failures | Use `Result<T>.Failure(...)` |

<!-- ci: markdown-only edits do not trigger the deploy workflow (paths-ignore). -->
