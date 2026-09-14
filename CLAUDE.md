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

## 0b. Frontend → API map (`docs/frontend-api-map/`)

**The Flutter mobile app's repo is NOT available here — do not ask for it.** Its entire API
usage is documented in `docs/frontend-api-map/`: a README index + 12 chapters covering every
screen and button → HTTP method + endpoint → the exact request keys the app sends (from its
`toJson()` code) → the response keys it actually reads (from `fromJson()`), plus the error
`code` values each screen special-cases and the offline sync replay contracts. Consult it
BEFORE changing any response shape, key name, enum value, or error code — deployed clients
parse exactly what those chapters record (including load-bearing misspellings like the
assistants query param `isAcitve`; never "fix" a wire spelling without a coordinated app
release). The README also lists endpoints no client calls today and modules that are
mock-only in the app (Messages, Homework, most of the parent app) — do not expect traffic
on them. Snapshot date 2026-09-12 (app branch `NewApp`): when a wire contract changes after
that date, the matching chapter section must be updated in the same PR.

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
  (06:00 Africa/Cairo), file-registry GC `file-object-gc` (hourly — see §5.5). A new
  recurring job needs its own registration here.
- Swagger examples are **fully automatic — there are no per-endpoint or per-module providers**, and
  a new endpoint needs NO registration. Three filters cover everything: `AutoExampleSchemaFilter`
  (request-body example per DTO + "Allowed values: …" on every enum), `ResponseEnvelopeExampleFilter`
  (`{success, message, data}` per response) and `ParameterExampleFilter` (query-string examples, so
  imported Postman GETs arrive filled in). *(Corrected 2026-09-14: this section used to describe an
  `IEndpointExampleProvider` / `AddSingleton` pattern. No such provider is registered anywhere in
  the solution — the mechanism was replaced by these filters. Do not go looking for it.)*
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

**One live violation was found and removed 2026-09-14**: `UserLoginActivity.UserId` carried
`[ForeignKey(nameof(User))]` *and* a Fluent `HasForeignKey(...).OnDelete(NoAction)`. The emitted
FK happened to be NO ACTION anyway (`NoAction` is also what EF infers for this shape), so removing
the annotation produced **no schema change** — verified with
`dotnet ef migrations has-pending-model-changes`. That is the point: the combination is a latent
trap, not a live outage, and it only becomes one the day someone changes the Fluent `OnDelete` and
watches it be ignored. **Grep for `[ForeignKey]` before adding an `OnDelete` to an existing FK**,
and when you remove an annotation, prove the no-op with the model-changes gate rather than
assuming it.

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

### 5.5 File Handling — Central FileObject Registry (shipped 2026-07-16, merge `07aba66`)

ALL uploaded files (video thumbnails, video PDF attachments, exam-question images, the
sign-up national-ID image) live in ONE **private** blob container (`uploads`;
`AzureBlobStorageOptions.UploadsContainerName`) and are tracked one-row-per-file in the
**`FileObjects` registry** (opaque `PublicId` GUID; `OwnerUserId`; denormalized `TeacherId`
tenant set server-side from the JWT; `Category`; `Status`; `BlobPath` — never exposed).
The storage account has `allow-blob-public-access=false`: an anonymous blob URL returning
401/403/409 is the INTENDED state, not a bug — never re-enable public access.

- **Reads**: only via `GET /api/files/{fileId}` (`FilesController`, `[Authorize]`), which
  re-authorizes on EVERY fetch — owner → SuperAdmin → category policy (`FileAccessService`,
  fail-closed) — then 302s to a short-lived SAS (`UploadsSasLifetimeMinutes`, default 240).
  Category policies: `VideoPhoto` (the video's cover image — renamed app-wide from
  "thumbnail" 2026-07-16) / `VideoAttachment` / `VideoExamQuestionImage` = teacher-tenant OR
  a student the OWNING VIDEO is scoped to (`IsScopedStudentOfVideoAsync` →
  `GetOwningVideoAssetIdForFileAsync` + the module's canonical `IsStudentInVideoScopeAsync`,
  which also enforces the Published + PublishDate gate — Draft/scheduled videos never expose
  files to students); `OnlineExamQuestionImage` = tenant OR a student in the exam's LIVE
  assigned set (tenant-scoped EXISTS, `IsQuestionImageAssignedToStudentAsync`);
  `NationalIdImage` = owner+admin only (no resource policy; excluded from
  `FileConstants.UploadableCategories` — created server-side during sign-up);
  `ExamAttachment` (added 2026-09-13) = tenant OR a student with an obligation on one of the
  exam's occurrences AND the exam's release gate open (§7.9). The student
  video list (`GetVisibleVideosForStudentAsync`) returns `videoPhotoUrl` + `attachment` per
  row, batch-resolved (no N+1).
- **Writes**: frontend uploads via `POST /api/upload` (multipart `files` + required
  `category`) → 201 `{fileId, url, ...}` with `Status=Pending`; resource create/update then
  passes `fileId` and the service attaches it via `IFileAccessService.ResolveForAttachAsync`
  (ownership/tenant + category match + 409 claim-stealing guard) INSIDE the resource's
  transaction. Replace/delete on `/api/upload` are fileId-based and ownership-guarded.
- **Lifecycle / cleanup**: `Pending → Attached → Detached`. Deleting/replacing a resource
  DETACHES its files in the same transaction (`DetachAsync`; e.g.
  `VideoService.DetachVideoFilesAsync`, exam replace/delete). Blob deletion happens ONLY in
  the hourly `FileObjectGcJob` (`file-object-gc`): reaps every `Detached` row + `Pending`
  rows older than `UploadsPendingGraceHours` (24), blob-first then row, retried next sweep.
  **Never hard-delete a FileObject row or its blob inline** — that orphans the blob forever
  (only registry-backed blobs are GC-visible). `VideoAssetRepo.DeleteVideoAsync` carries a
  defensive detach `ExecuteUpdate` so the NoAction FK can never block a video delete.
- **Consequences shipped with this change**: videos create/update/replace-video-photo are
  **JSON** (`videoPhotoFileId` / `attachmentFileId` / `ReplaceVideoPhotoRequest` on
  `PUT /api/videos/{id}/video-photo`) — no more multipart or `IFormFile` on those routes; the
  attachment-download endpoint was removed (the gated URL is used directly); `User.IdImage`
  varbinary → `IdImageFileId` FK; `VideoAttachments` table dropped (folded into the registry,
  category `VideoAttachment`, back-ref `FileObject.VideoAssetId`);
  `OnlineExamQuestion.ImageFileId` / `VideoExamQuestion.ImageFileId` added (requests carry
  `imageFileId`, responses carry the gated `imageUrl`). Migrations
  `20260716140711_FileObjectRegistry` + `20260716151304_RenameThumbnailToVideoPhoto`
  (column `VideoAssets.ThumbnailFileId` → `VideoPhotoFileId`; "thumbnail" is "video photo"
  everywhere — enum `FileCategory.VideoPhoto`, DTOs, route, resx keys `VideoPhoto*`).
- **Adding a new file-bearing feature** = add a `FileCategory` value + a policy branch in
  `FileAccessService.IsReadAuthorizedAsync` + a named EXISTS repo method — nothing else.
  Unhandled categories are DENIED by default.
- **A category policy must include the module's own visibility switch, not just the resource
  gate** (added 2026-09-14). `ExamAttachment` checked the student's obligation and the release
  gate but never `TeacherConfiguration.StudentVisibilityExamDefault`, so a student holding a
  fileId from before the teacher hid exams kept fetching the paper — a released exam stays
  released, and hiding the module reached the list and nothing else. `FileAccessService
  .IsReleasedExamAttachmentForStudentAsync` now denies first on that flag, **fail-CLOSED on a
  missing configuration row** (unlike the video gate, which fails open) and read off the config
  row the method already loads, so the gate costs no extra query. The list side
  (`ExamHomeworkService.LoadReleasedExamAttachmentsAsync`) applies the **identical expression** —
  §7.9's "two gates, keep them in step" now covers three facts, not two. See §8 BUG-29.
- **`Content-Disposition` is RFC 5987-encoded** (shipped 2026-09-14,
  `AzureBlobFileStorageService.BuildContentDisposition`; the header travels in the SAS as `rscd`).
  Whenever a download name is not pure printable ASCII the service emits BOTH an ASCII
  `filename="…"` fallback **with the extension carried across** (the OS picks the viewer off it,
  so an Arabic-named paper stripped to an empty stem still opens — stem falls back to `file`) AND
  `filename*=UTF-8''…`. A name that is already ASCII produces the byte-identical header it did
  before, so no existing download changes behaviour.
  - **Why**: this app's primary language is Egyptian Arabic and teachers name papers «امتحان
    الشهر الأول.pdf». RFC 6266 confines the plain `filename` parameter to ISO-8859-1, so raw
    UTF-8 there is non-conformant and Safari on iOS — how most students open a PDF — mangles it
    or substitutes a generic name.
  - **It also closed a CR/LF header-injection path.** ASP.NET Core *decodes* RFC 5987
    `filename*` on UPLOAD, so a crafted multipart part can put a real CRLF into
    `IFormFile.FileName`, and `Path.GetFileName` keeps it on Linux. `FileObject.OriginalName` is
    still stored **unsanitised** by design (it is the teacher's name, shown back to them) — **the
    encoder is the barrier**. Any new code that puts `OriginalName`, or any user-supplied text,
    into a header must go through it. Never interpolate a stored name into a header directly.
  - Do NOT swap `EncodeRfc5987ExtValue` for `Uri.EscapeDataString`: its pass-through set is RFC
    3986 "unreserved", a different list that has already moved once across runtimes, and
    `' ( ) *` are not `attr-char`.

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

### 6.5 Every recurring job that writes needs a kill switch and clamped bounds

A recurring job runs forever, against production, with nobody watching. Bind an options class
from configuration (`AutoAbsentOptions`, `AdminInsightsOptions`, `SessionNameReconcileOptions`)
so it can be tuned and STOPPED from App Service settings in one save — no redeploy, no DDL. The
pattern, uniformly:

- **`Enabled` is checked FIRST, before any statement runs**, so a disabled job writes nothing
  rather than doing a pass that repairs nothing. Log the fact and return.
- **`CronExpression` comes from the options**, read in `Program.cs` at
  `RecurringJob.AddOrUpdate(...)`; the `TimeZone` stays `Africa/Cairo` in code.
- **Clamp the numeric knobs at the point of use, UPPER bound included.** A lower bound alone
  protects nothing: the failure mode is a typo in an App Service setting, and the dangerous
  direction is *up*. `SessionNameReconcileJob` clamps `BatchSize` to `[1, 5000]` and
  `MaxBatchesPerTable` to `[1, 1000]` precisely because a batch above SQL Server's ~5000-lock
  escalation threshold turns the job into the `AttendanceRecords` table-lock incident it exists
  to avoid (§7.10).
- Defaults must reproduce the pre-configuration behaviour **exactly**, so adding the options
  class is a no-op until someone sets something.

---

## 7. Module Status & Active Work Items

### 7.1 Module 8 — Messaging (P0 defects outstanding)

| ID | Defect | Status |
|---|---|---|
| P0-A | Auth endpoints commented out on `MessagingController` | **Fixed 2026-07-16** (commit `000f009`) — class-level `[Authorize]` and the `[ModulePermission("Messaging","SendManual")]` / `"ViewHistory"` gates on `send` / `history` / `history/{id}/resend` uncommented; both are registered permissions and the tenant is still JWT-forced by `TenantScopeFilter`. See §8 BUG-13. Do not re-comment. |
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

**Auto-absent nightly sweep — unmarked/held ⇒ Absent (shipped 2026-07-24).** DELIBERATE reversal of
the old "unmarked = unknown, never inferred" policy (requested: a student the teacher skipped, or left
on hold, should count absent). This is NOT the historical "harmful silent Absent" per-mark default that
`ValidateCallerStatus` still guards against — that was an ungated default on an explicit mark; this is a
gated, equivalence-safe, auditable background job. Do NOT "fix" it as a regression.
- **Job**: Hangfire recurring `attendance-auto-absent` (`AttendanceConstants.AutoAbsentJobId`, default
  02:30 Africa/Cairo) → `AutoAbsentDispatcherJob` fans out one `IAutoAbsentJob.SweepTeacherAsync(teacherId)`
  per teacher on the `auto-absent` queue → `AttendanceAutoAbsentService` (Application; the job is a thin
  wrapper, §6.2). Config `AutoAbsentOptions` ("AutoAbsent" section): `Enabled` kill switch,
  `EffectiveFrom` (**non-retroactive** — never marks class days before this; default the go-live date
  2026-07-24), `LookbackDays` (rolling catch-up window, default 14), `CronExpression`.
- **What it writes**: for each active-roster student obligated on a PAST occurrence (assigned on/before
  the class day, BR-ATT-001) with no record, an `Absent` row flagged **`AttendanceRecord.IsAutoAbsent`**
  (migration `20260724184931_AddIsAutoAbsentToAttendanceRecords`, `bit NOT NULL default 0`); and it rolls
  an unresolved `Held` record forward to `Absent` (same flag). Then it recomputes the student's
  `StudentAbsenceCounter` from records, refreshes `OccurrenceStatus`, and (post-commit, best-effort)
  fires the normal absence notification + `ReconcileExamsForSessionOccurrenceAsync`.
- **Equivalence-safe (the reason it's a job, not a per-occurrence write)**: a linked session's equivalent
  occurrence (same `(WeekStartDate, DayPositionIndex)` slot) can fall on a LATER weekday (Sun≡Mon), so it
  marks a student absent for a slot ONLY once the **max occurrence date across the home + linked sessions
  has passed** (`GetMaxOccurrenceDateForSlotAsync`), and skips any student with a record on the occurrence
  OR any equivalent occurrence (`GetEquivalentOccurrenceIdsAsync` + the batch equiv check — they attended
  a linked class). Idempotent: a slot with an existing record for a student is never re-marked.
- **Flip on late scan (mark path)**: `MarkAttendanceAsync`/`BulkMarkAttendanceAsync` no longer 409 an
  auto-absent as a duplicate. A same-occurrence `IsAutoAbsent` row is **overwritten** by a later mark
  (teacher marking the morning after isn't forced into Edit). An `Absent` on an **equivalent occurrence**
  (auto OR manual — physical attendance is ground truth) **flips to `CrossSessionPresent`** with the
  attended session/date when the student is scanned Present there — logged as an `AttendanceEditLog`,
  `IsAutoAbsent` cleared, counter recomputed so the "was absent last time" alert (driven by
  `ConsecutiveAbsences`) does NOT surface for the same logical class instance. Shared helper
  `ApplyReconciliationAsync`; edit-reason resx keys `AutoAbsent*` / `AbsentFlippedToCrossSessionPresent`.
- **Scope note**: only CURRENTLY-active assignments are swept (matches the occurrence-status roster); a
  student unassigned since the class day is not retro-marked.
- **`GraceDays` (added 2026-09-10, default 1).** The sweep may only infer an absence on a class day
  older than `localToday - GraceDays` (gate `effectiveMax >= sweepCutoff`; the candidate query is
  bounded by the same cutoff, so it also loads less). Attendance is routinely taken OFFLINE and the
  queued marks only reach the server when the app is next open AND online — at 0 an evening class was
  swept ~6 hours later, overnight, with the app closed, so the sweep wrote absences for a class the
  tutor had already marked. Config-only (`AutoAbsent__GraceDays`); 0 restores the old timing.

### 7.2c Offline attendance + payment sync — the write path must never lose a mark (2026-09-10)

`POST api/Attendance/sync` is the path EVERY offline class takes (a single queued mark uses
`POST mark`; two or more go through the batch). Two short-circuits in it were silently discarding
marks in production — a class showed Present on the tutor's phone and Absent to the parents:

- **A differing server row is not automatically a conflict.** `SyncOfflineRecordsAsync` returned
  `IsConflict` for ANY existing record with another status, never reaching `MarkAttendanceAsync` —
  which has overwritten a system `IsAutoAbsent` row and resolved a `Held` row since the sweep
  shipped. Combined with the 6-hour sweep race above, a whole offline class came back as
  unresolvable conflicts. Now: same status → success; auto-absent-Absent OR Held → fall through to
  the shared mark logic; anything else → conflict (a real tutor decision). Keep the fall-through —
  do not re-add a pre-check that bypasses `MarkAttendanceAsync`.
- **A replay is never withheld for a prompt.** The path now forces `AbsenceAlertConfirmed = true`
  and reports the alert as INFORMATION on the success entry (`SyncEntryResultDto.AbsenceAlertRaised`
  + `AbsenceAlertInfo`). Previously a Present mark for a student with `ConsecutiveAbsences > 0`
  returned `Record = null` and recorded NOTHING; a background drain has no human to answer, the app
  pre-confirms from a CACHED list row that predates the sweep, and a tutor with the absence pop-up
  off never pre-confirms at all. The loss hit exactly the students who were absent last time (~8 of
  100) and was self-reinforcing — the same faces every week. REQ-ATT-057/058 keep their teeth on the
  INTERACTIVE path (`POST mark`), which is untouched. **Do not "restore" the withholding.**
- Payments: `PaymentSyncEntryResultDto.ErrorCode` (the `Result.Code` message key, plus
  `StudentNotAssignedToSession` / `StudentNotFound` for the pre-collect checks) so the app can tell a
  collector holding cash what to DO. `CollectPaymentDto.CollectionNote` always worked on this
  endpoint — the app was sending the key `note`, which bound to nothing; that is why partial collects
  were online-only.

App side (`edvanz-mobile-app`): an op in `conflict` / `needsConfirmation` means NOTHING was recorded,
so it must never be counted as "saved offline" nor painted on the student list (both the scan-result
count and the pending overlay used to do exactly that). A dated student-list snapshot carries THAT
day's statuses — serving it for another day made already-marked students unmarkable and silently
dropped them from bulk-mark, so a mismatched snapshot day now yields membership with every status
nulled. Entry points: home banner, side-menu row with a badge, and a post-drain dialog — the screen
is "Unsent records", never "sync center", and **no user-facing copy anywhere says "roster"**.

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
  `TeacherStudent` (roster) record is a distinct, re-pointable step. `accept` takes
  {`teacherStudentId?` | `studentCode?`} exactly like `bind` (2026-07-16 — both go
  through the shared `ResolveRosterTargetAsync`): a supplied target links atomically
  ("Accept & link") and one that does NOT resolve **fails the accept** (it must never
  silently downgrade to a plain accept — that was the "accept succeeded with a garbage
  code" prod bug); with BOTH omitted it accepts UNBOUND (`TeacherStudentId = null` —
  Active but **Not linked**: connected, sees NOTHING, since every module joins through
  that FK). `POST {linkId}/bind` {`teacherStudentId?` | `studentCode?`} links or
  re-points ("Change linked student"); `POST {linkId}/unbind` clears the binding yet
  stays Active. Both are `Student/Edit`, return the updated `LinkedStudentListItemDto`,
  and re-run the one-account-per-record guard. **Two DIFFERENT codes, never conflate:**
  `studentCode` on these bodies is the TEACHER's roster code
  (`TeacherStudent.StudentCode`, per-teacher unique, e.g. `A12`) — NOT the student's
  globally-unique account code (`StudentUser.StudentAccountCode`, the 10-char
  `studentAccountCode` on link rows). Passing the account code returns 400
  `StudentAccountCodeNotRosterCode` (dedicated message; the generic
  `RosterStudentNotFound` confused the frontend into reporting it as a bug). A wrong
  CODE returns 404 `TeacherStudentCodeNotFound` ("Wrong student code was provided…");
  `RosterStudentNotFound` is now only for a wrong `teacherStudentId`. Message TEXTS
  avoid the word "roster" (user-unfriendly); the `Roster*` resx keys and
  `rosterStudent*` DTO fields keep their names — shipped wire contract. Both DTOs
  reject unknown JSON fields (`JsonUnmappedMemberHandling.Disallow`) so a typo'd field
  400s instead of silently succeeding. `IsLinked` (= `Active && TeacherStudentId !=
  null`) is exposed on the teacher `LinkedStudentListItemDto` AND the student
  `StudentDashboardTeacherDto` — distinct from `Status`; do NOT add a `LinkStatus`
  member for it (the filtered-index literals `[LinkStatus] IN (1,3)` are hand-synced).
  No migration (`TeacherStudentId` already nullable).
  `IStudentLinkNotifier.NotifyLinkBindingChangedAsync(linked)` fires on bind/unbind.
  Accept never auto-matches by the request's typed code — the client passes
  `suggestedMatch.teacherStudentId` for one-tap "Accept & link".
- **End-of-link audit**: `RespondedByUserId` records who accepted/rejected;
  `RemovedByUserId` records who ENDED the link (student on Unlinked/
  CancelledByStudent, teacher/assistant on RemovedByTeacher). Plain columns, no FK.
- **Visibility concept unchanged**: `TeacherConfiguration.StudentVisibility*`
  flags are still returned per dashboard entry and still gate the per-module
  student endpoints (attendance/videos today; payments/exams/homework when built).
- **THIRD axis — session assignment (added 2026-09-05).** Connected → Linked →
  **Assigned** are three SEPARATE gates and only the first two were ever surfaced.
  Videos and online exams are targeted by session ONLY (`VideoScopeType` /
  `OnlineExamScopeType` are Session|SessionGroup — there is NO per-student scope), so a
  linked, bound student with `TeacherStudent.SessionId = null` gets an empty list with a
  **successful 200** on both, and the app could not tell that apart from "the teacher
  published nothing". `GET me/teachers/{teacherId}/home` now returns `sessionId` +
  `sessionName` (null = unassigned), resolved through the named repo method
  `ITeacherStudentRepo.GetAssignedSessionAsync(teacherStudentId, teacherId)` — tenant-scoped
  by matching `TeacherId` on BOTH the student row and the session row. Additive, no
  migration. The student app renders it as a "Session" row plus, when null, a note telling
  the student to ask their teacher; the note is suppressed when the teacher has hidden BOTH
  Videos and Online exams (it names those two modules, so promising them would be false).
  DELIBERATELY NOT guarded against a missing field on the client (product decision) — a
  rollback past this change would show the note to every student. Only the home aggregate
  carries it; `me/teachers` / `me/dashboard` were left alone on purpose (no consumer, and it
  would add a batch join to a hot list endpoint). Student-facing copy says **"Session" /
  «المجموعة»** — never "roster", which users do not understand.
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

**Proration rev 2 — ENROLLMENT-ANCHORED (2026-09-03; supersedes the 2026-09-02
first-attendance-anchored model).** The joining (first) month of a genuinely NEW enrollment is
priced ONCE, at assignment, from the teacher-LOCAL assignment day — **attendance never prices the
joining month** (an absence is a missed obligation, not a discount; consistent with the auto-absent
sweep treating assignment as the start of the attendance obligation). Methods
(`TeacherConfiguration.ProrationMethod`): `ByPercentage` = join-day tier; `ByClasses` = scheduled
classes from join day → month end ÷ total that month (0 remaining ⇒ 0-due anchor, born `Paid`);
`Manual` = full until a human sets it. All joining amounts snap to the nearest 5
(`SnapToNearest5`), clamped [0, full]. `ReapplyFirstAttendanceProrationAsync` and its
AttendanceService hooks were **deleted — do not reintroduce an attendance-driven re-price.**
`ComputeProrationSuggestionAsync` always recomputes from `GetEarliestAssignmentDateAsync` (min
AssignedAt across ALL sessions — survives moves) + current settings, never from the stored
fraction (the rev-1 echo made "reset to suggested" circular after a manual override — the
confirmed x1 bug). A human-set amount (`PaymentPeriod.IsProrationManual`, sole writer
`SetStudentProrationAmountAsync`) stays STICKY through every automatic path, is now ALWAYS
audited (period-linked `PaymentEditLog`, actor + suggested + set), surfaced on the lookup
(`prorationSetByName`/`prorationSetAt`) and lists (`isProrationManual`), and is cleared only by
PUT proration with `amount: null`. A settings save whose proration signature changed runs
`ReconcileProrationForExistingStudentsAsync` (idempotent, skips manual + any-cash anchors) and
returns a `ProrationReconcileSummary` on the config DTO so the app reports "N re-priced · M kept"
instead of recalculating silently. Carried never-paid anchors keep their stored fraction on a
move (re-applied to the new base) — no attendance lookup. **The pricing story is FROZEN on the
anchor row** (`PaymentPeriod.ProrationClassesTotal`/`ProrationClassesBilled`, migration
`20260903121426_AddProrationClassBasisToPaymentPeriods`, nullable — ByClasses only): every list
surface (by-status tabs incl. PAID rows, session hub rows, collect list) explains the price as
"Joined {date} · {billed} of {total} classes left · {amount} of {full}" (percent form for By%,
"Set by hand" for manual) via `GetAnchorPeriodInfoByStudentIdsAsync` + the FE
`buildProrationStoryLine` helper — collection never erases the story, and schedule edits/method
switches never rewrite history (display reads the frozen counts, not the live schedule). The
COLLECTOR-scoped ledgers tell the same story (increment 3): `CollectionRow` carries
`ProrationJoinedAt`/`ProrationClasses*`/`ProratedFirstMonthAmount`/`ProrationFullMonthAmount`
(full base reconstructed as `round(AmountDue ÷ fraction)`) filled by
`EnrichProrationTransparencyAsync`, rendered NAME-FREE (the ledger is already scoped to one
person — per-row names are noise; the who-set-it audit stays in the collect editor). Related
UX merge (2026-09-03): tapping an assistant on the collectors card opens ONE merged
wallet+ledger screen (`TeacherPaymentSessionCollectionsView` wallet mode) — balance card +
withdraw + hand-over history above the full ledger, opening on the "in hand now" scope (exact
window since the last hand-over, so rows sum to the balance) with an "all months" chip; the
collections endpoint's `from`/`to` accept EXACT instants bounding [from, to) when the RAW query
value carries a time component (the controller passes `exactRange` into
`GetCollectionsByMonthAsync`; date-only callers unchanged — inclusive whole days), and the wallet
response's `collections.sinceAt` anchors it (strict `heldSinceAt` for scoping). Fixed 2026-09-04:
the old `TimeOfDay != 0` inference misread the wallet day filter's midnight-to-midnight window as
date-only and served the whole NEXT day inclusively — never reintroduce value-based inference,
the raw text is the only carrier of that intent. The mobile cubit sends drawer/day bounds as UTC
instants ('Z' suffix): `CollectedAt` is UTC, day boundaries must cut on the backend's raw-UTC
`dayKey` that the day headers/nets/insights bucket by, and the earlier local-wall-clock echo
anchored "in hand now" ~3h late for Egypt devices (dropping post-hand-over rows from the sum).

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

**Assistant wallet & refunds.** Wallet rises on collect, falls on refund; refunds are
negative-amount entries in the same month log as collections
(`GetCollectorTransactionsInRangeAsync` + `GetCollectorRefundsInRangeAsync`, merged).
**Who a refund is charged to depends on its kind (changed 2026-08-24):** a **delete/edit**
(`Deleted` `PaymentEditLog`) is a CORRECTION — charged to the **original collector**
(the transaction's `CollectedByUserId`) whose figure it corrects, no fresh cash moves; a
**departure refund** (`Reversed` log, sole writer `ReverseDeparturePeriodAsync`) is a
physical PAYOUT — charged to the **performer who confirmed the departure**
(`EditedByUserId` on the log; `StudentDeparture.CollectedByUserId` also holds the
performer despite its name — their drawer handed the cash back, the original collector's
held cash is untouched). Migration `20260824190558` flipped historical departure rows.
`TotalCashCollected` = money in − money out for the month. **Withdraw** = tutor taking
cash (a `WalletResetLog`), reduces `CurrentBalance` only, distinct from a refund.

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

### 7.4a Removed collectors + the assistant's own payment screen (2026-09-05)

**A removed collector is HISTORY, not roster.** The tracking collectors card
(`GetTrackingAsync` → `CollectedByAssistant.Assistants`) is built from the WALLET roster
(`GetAllAssistantWalletsAsync`), and an assistant's soft-delete is terminal (`AssistantCleanupJob`
is a no-op) so their wallet row lives forever — a removed person used to surface on EVERY future
month as a silent "0 EGP / 0 payments" card. A wallet row is now included for month *M* only when
the collector is not removed, **or** the teacher-local `Assistant.DeletedAt` /
`CenterAssistant.DeletedAt` falls on/after `monthStart` — removed in July ⇒ visible in July and
every earlier month, gone from August on. This is DELIBERATE; do not "restore" the missing row.
Two invariants hold it together: (a) delete is blocked while the wallet holds cash and kills the
login, so a removed collector can neither collect nor confirm a departure afterwards — their
post-removal months are always empty and the hidden rows carry no money; (b) an **activity escape
hatch** keeps any row with a non-zero month total regardless of removal, so the visible cards always
reconcile with `CollectedByAssistant.TotalCollected` (which stays activity-driven over ALL
collectors). Surviving rows carry `isRemoved: true` and the app renders a muted "Removed" chip. The
activity-driven `byCollector` on `/collections/summary` was already correct and is untouched.

**The assistant's payments tab is the teacher's ledger, own-scoped.** `AssistantPaymentView` keeps
its cash-bag card + Collect + Student-leaving actions (no withdraw — teacher-only) but its list is
now the SAME ledger the teacher's assistant drill-in shows: `TeacherPaymentSessionCollectedCubit`
(scope chips "collections in wallet"/"all collections", month nav, day filter + day insights,
"collections only", amount tiers, day totals + nets, proration story rows, skeleton loading), driven
by the shared `PaymentWalletLedgerCoordinator` / `PaymentCollectionsLedgerList` /
`PaymentCollectionsFiltersSection` widgets that BOTH screens use — extend those, never fork them
back per screen. Two backend gaps closed with it:
- **Forced own-scope (was a tenant-internal IDOR).** `GET /api/v1/payments/collections` and
  `/collections/summary` never applied `AssistantScopeUserId()`, so an assistant with
  `Payment.ViewHistory` could omit `collectedByUserId` and read the tutor's WHOLE ledger, or forge a
  peer's id. Both now do `collectedByUserId = AssistantScopeUserId() ?? collectedByUserId`, mirroring
  the wallet / wallets / collector-summary endpoints. Teacher/SuperAdmin behaviour is unchanged.
- **`ModulePermission(..., alsoAllowPermission:)`** — an optional SECOND permission that also
  satisfies a gate (checked only after the primary fails, and never past a module-not-assigned
  failure). Widen-only by construction: no existing caller can lose access. Used on
  `/api/v1/payments/collections` so `ViewCollectorSummary` — the grant that already opens the
  assistant's wallet over the very same rows — passes too, otherwise the rebuilt tab would 403 for
  assistants whose tutor granted only that. Argument binding for the extra ctor string is positional
  through `ActivatorUtilities`; it was verified against all four attribute shapes (module+permission,
  +alsoAllow, roleOnly, module-only) before shipping.

### 7.4b Billing start date — tenant-wide billing floor (added 2026-09-03)

`TeacherConfiguration.BillingStartDate` (nullable, teacher-local, always first-of-month) is an
ONBOARDING billing floor: a teacher entering the roster in August while classes/billing begin in
September sets September and NO pre-September obligation exists. Null = off = historical behavior
byte-for-byte. Rules:

- **Generation clamp** (`BuildSessionPeriodsAsync`, both branches): the ladder starts at
  max(assignment date, floor) — the floor-clamped date is ALSO the proration join day (Aug 15 entry
  + Sept floor ⇒ Sept-1 join ⇒ day-1 tier / all classes ⇒ full first month). PerSession skips
  occurrences before the floor. The optional `billingStartOverride` parameter exists so the
  reconcile can dry-run a floor that is not yet saved — do not remove it.
- **Carried debt is NEVER the floor's business**: `IsCarriedForward`/`MovedFromSessionId` rows are
  agreed money, not generated months — the reconcile skips them and the DB2a pending-debt fold-in
  in `OnStudentAssignedToSessionAsync` clamps its delete cutoff to the floor so a pending month in
  [assignment, floor) re-attaches as arrears instead of being deleted-but-never-rebilled.
- **`ReconcileBillingStartAsync(teacherId, floor, dryRun)`** (runs on the caller's transaction):
  TRIM deletes pre-floor rows with no cash, not manual, not carried (kept rows reported as
  keptPaid/keptManual — cash and human decisions are ground truth); BACKFILL regenerates months an
  EARLIER floor newly allows for currently-assigned students via the shared generation pipeline
  (front-gap bounded — trailing gaps belong to the end-date backfill); RE-ANCHOR moves
  `IsProrationAnchorMonth` to the surviving first month (cash-free, non-manual anchors only),
  re-priced per the current method; counters fully recomputed per affected student. Summary
  (`BillingStartReconcileSummary`) is surfaced on the config-save response
  (`billingStartReconcile`, sibling of `prorationReconcile`) — never reconcile silently.
- **One-time self-service**: the teacher's first set is free; after that
  (`BillingStartDateSetAt != null`) changes 403 `BillingStartDateLocked` until support re-grants
  ONE change via `BillingStartDateChangeAllowed` (consumed on use). On the wire the update DTO
  field is NULLABLE ON PURPOSE (omitted/null = unchanged — teachers can never clear the floor;
  old app builds must not disturb it); re-sending the identical value is an idempotent no-op that
  neither trips nor consumes the lock. Config GET exposes `billingStartDate` +
  `billingStartLocked`.
- **Admin surface** (`AdminPaymentController`, SuperAdmin roleOnly):
  `POST /api/admin/payments/billing-start?teacherId=&date=&dryRun=` sets + reconciles in one
  transaction (dryRun default TRUE writes nothing; an admin set re-locks the teacher);
  `POST /api/admin/payments/billing-start/allow-change?teacherId=` re-grants the one-time change.
- Migration `20260903162852_AddBillingStartDateToTeacherConfigurations` (additive columns only).

### 7.5 Offline Exams (`/api/exams`) — during-session dates are PER SESSION (restored 2026-07-17)

Create/update contract (v1.3 — `docs/exams-api-guide.md`, `docs/exams-openapi.{json,yaml}`):
recipient stays `sessionIds` XOR `groupIds`, but a **DuringSession** exam anchors EVERY resolved
session — including each member session of a selected group — to its own picked class occurrence
via `sessionOccurrences: [{sessionId, sessionOccurrenceId}]` (ids from
`GET /api/exams/session-dates`, called once per session; the entries must cover the resolved
session set exactly; a pick may be in the past — the class's attendance back-fills the exam).
The single `examDate` belongs to **SeparateTime only**. Sending the wrong date field for the
delivery type is a 400 (`ExamDateOnlyForSeparateTime` / `SessionOccurrencesOnlyForDuringSession`).
Create and update validate through ONE shared pipeline (`ExamService.BuildSessionPlansAsync`) —
extend it, never fork it back into the two methods. **History (do not reintroduce):** the
original per-session design (`479a2e2`) was replaced with a single global `examDate` in
`2bc840b`; that was a regression (its commit message says "frontend-requested" — it wasn't the
planned design). Restored 2026-07-17 while keeping `2bc840b`'s keepers (groups recipient, home
pagination/scope, `teacherStudentId` write keys). PUT `/api/exams/{examId}` mirrors create;
structural edits still 409 once results exist (`ExamHasResultsCannotRestructure`).

### 7.6 Subscription plans — ManagerialPlus ("Managerial + Parents", added 2026-09-03)

Three plans: `Full=1`, `Managerial=2`, `ManagerialPlus=3` (display "Managerial + Parents" /
«إداري + أولياء الأمور» — the enum identifier is the stable wire value, clients localize labels).
ManagerialPlus = Managerial rules (no student app accounts, no in-app parent accounts) PLUS the
public parent follow-up page. The plan → feature map lives in ONE place —
`SubscriptionPlanCapabilities` (Domain.Helpers) — consumed via
`ISubscriptionGateService.GetPlanEntitlementsAsync(teacherId)` → `{PlanType,
StudentAccountsAllowed, ParentFollowUpAllowed}`. **Never compare plan values inline at a gate
site**; a new plan is a change to the capabilities class + pricing only. Restrictions apply only
while the plan is Active/ExpiringSoon; expired/none = unrestricted (free-tier quotas gate
creation instead). `IsManagerialAsync` is kept as the legacy alias for
`!StudentAccountsAllowed` (both managerial plans return true). Related invariants:
- Settings gate: enabling `ParentPortalEnabled` false→true without the capability → 403
  `ParentPortalRequiresSubscription`; an already-true stored value never fails unrelated saves
  (the read-time gate `ParentPortalService.IsPortalEligibleAsync` keeps the portal closed, and
  re-opens it automatically on upgrade — the stored toggle is never rewritten by plan changes).
- Wire entitlements: `GET /api/subscription/status` carries `features
  {studentAccountsAllowed, parentFollowUpAllowed}`; the parent-portal summary carries
  `portalAllowed`. Clients gate screens ONLY off these (fail-open on absence) — the app never
  hardcodes plan semantics. Blocked messages: `ManagerialSubscriptionNoStudents` (Managerial)
  vs `PlanNoStudentAccounts` (ManagerialPlus) via
  `SubscriptionConstants.StudentAccountsBlockedMessageKey`.
- Pricing: flat `ManagerialPlusMonthlyPriceEGP` (seed 650) on `SubscriptionPricingSettings`;
  admin PUT pricing accepts the flat prices as NULLABLE (omitted = unchanged — older admin
  clients must not zero them). Renewal amount is plan-aware (flat for managerial plans).
- Admin: `POST /api/admin/subscriptions/activate-managerial-plus` (same body as
  activate-managerial; `RemoveExistingLinks` severs student/parent-ACCOUNT links only, NEVER
  parent-portal follow-up grants).
- Centers: symmetric third slot type — `ManagerialPlusTeacherSlots` +
  `StudentCapacityUnderManagerialPlus` on CenterSubscriptions/-Requests,
  `ManagerialPlusTeacherSlotPriceEGP` (seed 65) on center pricing,
  `FreeTierManagerialPlusTeacherSlots = 0`; slot checks go through
  `CenterService.SlotsForPlan` (the single three-way switch). `Teacher.CenterPlanType` may be
  ManagerialPlus; the gate reads it through the same current-subscription projection.
- Migration `20260903154829_AddManagerialPlusPlan` (additive columns + seeded-row updates).

### 7.7 Parent portal sign-in — an unknown student code is REJECTED, not swallowed (reversed 2026-09-11)

**DELIBERATE REVERSAL of the original enumeration design. Do NOT restore the silent behaviour.**
The portal (`~/Desktop/Edvanz-Application/EdvanzParentPortal`, own git, manual FTP) asks a parent
for TWO codes; the teacher's own WhatsApp share message asked for ONE (the teacher code), so
parents arrived not knowing the second, guessed, and `RequestAccessAsync` answered a nonexistent
`StudentCode` with the byte-identical "pending" payload a real request gets — **writing nothing**.
The portal's `/pending` then treated every unrecognised state (including `none`, i.e. *no row
exists*) as "still waiting" and refreshed every 15s **forever**. The parent saw «طلبك اتبعت»; no
teacher ever saw a request. Reproduced on prod 2026-09-11: fake code → `pendingCount: 0`, real code
→ inbox row, **identical screen for both**.

- **Unknown student code → 404 `ParentPortalStudentCodeNotFound`**, an inline error under that
  field exactly like a wrong teacher code. Enumeration is now a **budget, not a blanket**:
  `UnknownStudentCodeRepliesPer{Device,Teacher}PerHour` (8 / 40, `IDistributedCache`) meter honest
  answers and revert to the old neutral payload once spent — fail **open** on a cache error. Student
  name/code/id are still only returned on an `active` result: *that* a code exists is answerable,
  *whose* it is is not.
- **Trust is evaluated BEFORE the post-rejection cooldown** (`ParentPortalService` step 5b, then
  5c). It used to be the other way round, which meant the one remedy the product advertises — the
  teacher adding the parent's number to the student record — silently did nothing for 24h. The
  cooldown now answers 409 `ParentPortalRequestPreviouslyRejected` and names that remedy.
- **A live PENDING row is RE-EVALUATED for trust and promoted in place.** The `existing` branch
  returned "still waiting" without re-checking anything, so a parent who first submitted with no
  phone and then supplied one — or whose TEACHER added the number to the student record — stayed
  queued forever and only a manual approval could release them. That is the same remedy the cooldown
  message and the waiting screen both advertise, silently doing nothing. `GetLiveByStudentAndDeviceAsync`
  loads the row TRACKED with the comment "the request path may promote this row to Active"; nothing
  ever did. Promotion sets Active/RespondedAt/AutoApproved/Origin, stores the number that earned it
  (so a later device change is re-admitted), leaves `RespondedByUserId` NULL, and on a write failure
  returns pending rather than lying. Trust lives in ONE place — `EvaluateTrustAsync`, shared by the
  new-request and promote paths; never re-inline it, or the two answers drift.
- **Phone REQUIRED** (`ParentPortal__RequirePhone`, default false for deploy ordering exactly like
  `RequireParentName`; flip BOTH after the portal upload). It is the only thing that admits a parent
  with zero teacher involvement, the only detail a teacher can recognise, and the only way back in
  after a lost device cookie — which iOS in-app browsers drop routinely.
- **The inbox row is now written for EVERY request**; only the push is batched to one per hour
  (`suppressPush`). The old early-return wrote nothing at all for the 2nd..Nth parent in an hour —
  and `Firebase__CredentialsPath` on prod is a 1-char placeholder with no vault secret, so FCM
  no-ops platform-wide and that row is the ONLY signal a teacher gets.
- Portal side: `/pending` renders **only** a real `pending` (anything else → "no request waiting" +
  a way back to the form); a `?retry=1` route that clears the remembered state; API `code` mapped to
  the FIELD that caused it (`signin_error_field`); student code upper-cased with inner spaces
  stripped; phone validated locally against a PHP mirror of `EgyptianPhoneNumber.Normalize` (keep
  the two in step); the per-hour throttle keyed **per IP+device** (Egyptian carrier CGNAT made a
  per-IP bucket punish unrelated parents) and 10 distinct codes per 30 min.
- **`includes/mock.php` returned 404 for state `none` while the real API returns 200 + `state:
  "none"`** — that fixture lie is *why* the waiting-screen bug survived review. Fixtures mirror the
  API, never the convenient behaviour.

**Same-day follow-up audit — five more defects on this path, all fixed 2026-09-11:**

- **SIBLINGS: a device may hold a live grant per child.** Reads resolved a device to its NEWEST
  active grant and required the route id to equal it, so a parent who signed in for a second child
  could never reach the first again — re-entering the older code still landed on the newer one, and
  the only escape was to end following altogether. `GetActiveByDeviceAndStudentAsync` authorizes the
  route id against the device's OWN grants (identical strictness, finally correct for two children),
  `GET /access?rosterId=` selects which child the payload describes, `students[]` lists them all,
  and the portal renders switcher chips in the app bar — whose doc comment had promised exactly this
  since day one. Selection lives in the PHP session, is re-synced from what the API actually
  returned (a revoked child degrades to the other rather than erroring), and the access cache key
  carries the child id — keying it on "access" alone serves the previous child for a minute after
  every switch.
- **The waiting screen polled twice over.** `<meta refresh 15s>` AND a 5s JS fetch, each a full
  render plus an uncached API call ≈ 960 requests/hour per waiting parent, indefinitely. portal.js
  now REMOVES the meta tag when it takes over and backs off 5s → 60s (resetting on tab focus); the
  no-JS fallback is 30s.
- **`ResolveTeacherHeaderAsync` cost FIVE round-trips** — one name and one label via the bulk
  dashboard loader, on every request and every poll. Now one query, `GetPortalTeacherHeaderAsync`.
  Deliberately uncached: it carries the config that gates eligibility and visibility.
- **Phone capture from the inbox.** `BulkResolveAsync` captured NOTHING, the worst possible place
  for that gap — the select-all lane exists precisely because a whole class arrives at once, so the
  teacher's highest-volume action guaranteed every one of those parents would need approving again.
  Bulk now fills EMPTY records only (`savePhoneToStudent`, ON by default in the app, NEVER
  overwrites — nobody judges forty "mother or father?" questions in one tap) and reports
  `phonesSaved`. Single approve may REPLACE a differing number via a separate
  `overwriteStudentPhone` flag behind its own confirmation naming both numbers; the number on file
  may be a second parent rather than a stale one, and only the teacher knows which.
  `studentParentPhone` is exposed on the list row (only when it differs) so that choice is informed.
- The approval sheet rendered a phone icon beside an empty line for requests carrying no phone, and
  the missing-parent-phone nudge lived only in Settings — it is now on the inbox itself, where the
  teacher is actively approving people who would not have needed approving at all.

### 7.8 Videos watched by class, exam grade chips, and the day a scan belongs to (2026-09-11)

**A scan marks the day the REGISTER is on, never "today".** `TeacherAttendanceQrScanView`
builds its OWN roster cubit, and it was never handed a date — so every scan landed on the
teacher's current local day no matter which class day was open. Opening an exam's class day
and scanning wrote the mark onto the most recent class instead: the exam still read absent,
and a day the student may not have attended gained a Present nobody made. Tapping a row by
hand was always correct, which is why it looked random. The header said only "Take
attendance" — no class, no date — so nothing on screen could catch it. `occurrenceDate` is
now threaded route → view → cubit (offline scans inherit it too: the queued op stores the
date from the request), and the header names the class and the day, spelling out any day
that is not today. NOT in the error colour — editing an old register is normal and the exam
screen sends you there deliberately. **Never let a sub-screen that builds its own cubit
re-derive scoping context the caller already had, and never let a screen write to a day it
does not name.**

**`GET /api/videos/{videoAssetId}/analytics/by-session`** — one row per class with
in-scope / watched / unseen / completed, single grouped query, not paginated. Rows are keyed
on the student's own `TeacherStudents.SessionId` — the SAME column the audience resolves
through — so a class reachable both directly AND through a scoped group is counted once and
the rows always sum to the header. `GET .../analytics` gained `sessionId` / `sessionGroupId`
filters (the narrower wins) and returns `sessionId` per row. The analytics session LABEL now
comes from `Sessions` via that column rather than the student's active
`StudentSessionAssignment` (a denormalized snapshot a rename never updated), which also
removed a LEFT JOIN that would have duplicated a student carrying two active assignments.

**Exam session roster `?graded=true|false`** backs the grade screen's All / Graded / Not
graded chips. It filters the SERVER page — the roster pages, so a chip narrowing only the
loaded rows would hide everyone past page 1 behind a number claiming otherwise — using the
SAME predicate `ExamService.ComputeStats` uses for `GradedCount` (flag AND value), so the
chip and its list can never disagree by one.

**Video duration is learned, and both places matter.** The app scrapes it from the YouTube
link on paste (`YoutubeDurationFetcher`, key-less oEmbed → watch page) and the student's
player reports it. `start-watch` fires on the play transition, before the player's metadata
is necessarily loaded, so it often reported 0 — and a video with no length reports NO
completion percentage to anyone (watch SECONDS are unaffected; both clamps already skipped a
zero duration). The stop/progress report now also carries it, **first-learn only**
(`stored == 0`): refining a known value there would write on every pause, because two
players can disagree by a second and each report sits inside the ±5% tolerance. Refinement
stays on `start-watch`. When the scrape comes back empty the teacher can type the minutes —
that box LATCHES on manual entry, because keyed off "is the length known" the first digit of
"30" makes it known and swaps the box for the read-only chip mid-keystroke.

### 7.9 Offline exam paper (attachments) + the watch threshold (2026-09-13)

**The exam paper.** A teacher uploads the questions (PDF or photos) so students can review them
after the exam. Files ride the existing `FileObject` registry (§5.5) under the new
`FileCategory.ExamAttachment`, back-referenced by `FileObject.AssignmentTemplateId` — the second
one-to-many case beside `VideoAssetId`. Migration `20260913161420_AddExamAttachments` (additive
columns + FK + index only).

- **Endpoints live OUTSIDE create/update on purpose.** `PUT /api/exams/{id}` 409s structural edits
  once any result exists (`ExamHasResultsCannotRestructure`), and uploading the paper AFTER the exam
  is the entire point — so `POST {examId}/attachments`, `DELETE {examId}/attachments/{fileId}` and
  `PUT {examId}/attachments/release` are their own surface. `CreateExamDto.AttachmentFileIds` exists
  for a teacher who prepares the paper in advance; it is NOT on `UpdateExamDto`.
- **Release is keyed on the LAST class, never the student's own.** A DuringSession exam anchors each
  session to its own class occurrence, so one class may sit it Monday and another Wednesday —
  releasing per-student would hand Monday's class a paper Wednesday's class has not sat yet.
  `AttachmentsReleaseBaseAt` = UTC instant of teacher-local midnight following MAX(occurrence
  DueDate); the effective release is that **plus `TeacherConfiguration.ExamAttachmentReleaseDelayHours`**
  (default 48, teacher-configurable in Settings → Exams). Only the BASE is stored, so changing the
  delay moves every exam at once with no reconcile pass. Recomputed on create, on a structural PUT,
  and on the first attach (exams predating the feature have a null base and would otherwise never
  release).
- **Visibility is a pure function**, never a stored flag:
  `AttachmentsReleaseOverride ?? (utcNow >= ReleaseAt)`. `null` = follow the schedule (this is what
  makes the teacher's switch turn itself ON when the delay elapses), `true` = released early,
  `false` = held back. There is no background job to drift. On the wire `override: null` is a REAL
  value meaning "follow the schedule" — not "unchanged".
- **Two gates, both load-bearing.** The student LIST omits an unreleased paper entirely
  (`GetTemplatesWithReleasedAttachmentsAsync`), and `FileAccessService` refuses the URL
  (`IsExamAttachmentVisibleToStudentAsync`). Both express the same predicate — keep them in step.
  Fail-closed.
- **THREE facts, not two** (added 2026-09-14). The predicate is
  `StudentVisibilityExamDefault` **AND** the student's obligation **AND** the release gate. The
  module switch was missing from both sides: release is permanent, so a student who had ever been
  handed a fileId kept the paper after the teacher hid exams. Added to
  `FileAccessService.IsReleasedExamAttachmentForStudentAsync` and, identically, to
  `ExamHomeworkService.LoadReleasedExamAttachmentsAsync` — fail-CLOSED on a missing config row on
  both sides, and no new query on either (the config is already loaded for the delay). See §8
  BUG-29.
- **The create path REFUSES an over-cap paper, it does not truncate it** (fixed 2026-09-14).
  `CreateExamAsync` did `.Distinct().Take(MaxAttachmentsPerExam)`, so twelve photographed pages
  created an exam whose paper stopped at page ten, with a 201 and nothing on screen saying so —
  while the dedicated endpoint answered the identical rule with 422 `ExamTooManyAttachments`. The
  count is now checked in step 1, before the transaction opens, and returns the same key and the
  same 422. A truncated question paper is worse than none: the student cannot tell which it is.
  See §8 BUG-28. **Two paths enforcing one rule must give one answer.**
- **Delete.** The FK is NoAction and the exam is HARD-deleted, so `DeleteTemplateAsync` detaches the
  papers inside its transaction (`DetachExamAttachmentsAsync`). DETACH, never delete the row — only
  registry-backed blobs are GC-visible. `FileAccessService.DetachAsync` now clears BOTH back-refs.
- **Parent portal is excluded on purpose**: `GetMyOfflineExamsAsync(..., includeAttachments: false)`
  from `ParentSectionComposer` — portal parents hold no JWT and could never fetch a gated file, so
  the query would be pure cost.
- **Protection**: last-class keying + configurable delay + an early-release confirm that NAMES the
  classes still to sit it (`sessionsYetToSit`) + reschedule auto-pushes the date + the student image
  viewer runs under `ScreenCaptureGuard`. A PDF handed to the system viewer necessarily leaves that
  protection — that is also what "or downloaded" asks for; upload as images if it must not.
- App: one shared `AttachmentPicker` (extracted from the video form, widened to images) and one
  shared `GatedFileOpener` — see BUG-18; never call `openUrl` on a gated URL again.
  **Correction (2026-09-14): the video form was never actually migrated** — the commit message
  said it had been and the extraction was real, but the old inline picker stayed in place, so the
  two drifted immediately. It is migrated now, passing **PDF-only** so video attachments did not
  silently widen to images. *A commit message is not evidence a call site moved; grep for the
  thing you claim to have deleted.*
- **HEIC is re-encoded at PICK time, always** (app, 2026-09-14). Every picked attachment IMAGE is
  decoded and re-encoded to JPEG regardless of size — quality 95 / 3000px long edge; the
  pre-existing over-limit path keeps quality 80 / 1600px. **Why**: Flutter/Skia cannot decode
  HEIC, HEIF or TIFF, HEIC is the iPhone camera default, and the backend allow-list ACCEPTS
  `image/heic` / `image/heif` — so an iPhone-photographed exam paper uploaded fine, rendered
  perfectly in the teacher's own browser preview, and was unviewable by every single student. The
  one place the failure showed was the only place nobody looked. A decode failure now **refuses
  with a specific message** rather than uploading the original, so the file is never accepted in a
  form no client can read. See §8 BUG-30.
  **Follow-up (open)**: the backend `UploadConstants` allow-list still accepts `heic`/`heif`/
  `tiff`. The app no longer sends them, but any other client can, and the server will store a file
  nothing renders. Decide whether to reject them at upload or transcode server-side.

**The watch threshold.** "Watched" stopped meaning "a `VideoAnalytics` row exists". `start-watch`
creates that row on the play transition with `TotalWatchSeconds = 0`, so opening a video and leaving
counted as watching it — on live data, 79 students "watched" one 62-minute video and 48 of them had
watched under a minute. A student now counts as having watched only past
`VideoConstants.WatchedMinSeconds(duration)` = `max(WatchStartedMinSeconds 60, ceil(duration ×
WatchStartedThresholdPercent 5 / 100))`.

- Applied in FOUR places or the surfaces disagree: `GetAnalyticsRowsForTeacherAsync` (adds
  `HasWatched`; `Seen`/`Unseen` split on it), `GetAnalyticsAggregatesAsync`,
  `GetAnalyticsBySessionAsync`, and the batched video-list counts via
  `VideoAudienceQueries.WatchedAnalytics`.
- **BUG-16 discipline.** The bar is always a computed `long` (single-video paths) or a per-row
  algebraic comparison `w >= 60 && (d <= 0 || w*100 >= d*5)` (the batched path) — never a captured
  bool, which EF folds into the literal `COUNT(NULL)` SQL Server rejects. The two forms are exactly
  equivalent (`ceil(d*5/100) <= w ⟺ d*5 <= w*100`); both branches were checked with
  `ToQueryString()` and the equivalence swept exhaustively.
- Wire is additive: `hasOpened` keeps its meaning, `hasWatched` is new, and `openedOnlyCount` lets a
  client split "opened and left" from "never opened". New `VideoAnalyticsStatusFilter.OpenedOnly` /
  `NeverOpened` back the Unseen screen's chips. Old builds keep working.
- App side: `_formatDuration` no longer floors sub-minute values to `"0m"` — 45 seconds reads `45s`.

### 7.10 A scan must reach the register — code resolution and the alert that ate it (2026-09-13)

Reported by one tutor as three complaints; all three are the same thing — a scan that never became
a record, with nothing on screen disagreeing. Her own words: «بنت كودها 8B … بيقولي الطالب مش موجود
ومش راضي يتاخد. روحت ادفعها الفلوس رضي ياخدها», «خدت الغياب من غير نت وظاهر ١١٣ طالب وهما ١٢٠».

**A scan resolves by EXACT code, and the exact match must be on the first page.** The scanner
searches, loads page 1 (10 rows) and then demands an exact `studentCode` on it. Codes are short and
share digits, so a code is routinely a substring of a dozen others: on her live 547-row linked
roster `8B` matched 16 codes and the student who owns it sorted 12th. Both server queries have
ranked the exact match first for a while
(`AttendanceRepo.GetPagedAttendanceStudentListAsync`, and now
`ExamHomeworkRepo.GetTrackingViewPagedAsync` — the exam scanner had the same hole ONLINE); the
OFFLINE snapshot slice had no equivalent and simply took the first ten substring matches. One
helper now expresses it for both offline paths — `rankExactCodeFirst` (`lib/core/utils/`) — and the
regression test carries the real 16-code fixture. Three codes on that one roster (`8B`, `8A`, `3C`)
were unreachable offline; five once marks reorder the snapshot. **Any new list a scan resolves
through must rank the exact code first, on the server AND in its offline slice.**

**Closing the "before you mark" alert may never discard the student.** `shouldShowAbsenceAlert ||
shouldShowPaymentAlert` opens a blocking dialog before the scan is queued — on her roster that is
**222 of 547 students (41 %)**. It was `barrierDismissible: true` with no cancel button, and every
one of its seven call sites read the resulting `null` as "do nothing": a stray tap at the classroom
door dropped the scan silently. That is the whole of «١١٣ وهما ١٢٠», and «روحت ادفعها الفلوس رضي
ياخدها» is the same bug seen from the other side — once her money was collected the alert stopped
firing, so the scan went through.
- The prompt now returns a non-nullable `AttendanceAlertDecision` (`status`, `wasDismissed`), so the
  compiler asks every call site what it wants. **Do not make it nullable again.**
- The barrier no longer closes it. Exits are the X and Back, both routed through one handler.
- Closing resolves into `intendedStatus` — the mark the tutor was already making — and the caller
  reports it BY NAME (`teacher_attendance_alert_dismissed_queued` / `…_marked`). A hint under the
  buttons says so before the act.
- **The one exception is the server-forced absence confirmation** (`forceShowAbsence`,
  REQ-ATT-057/058): there a dismissal must record NOTHING, so it warns first and names the student.
  That is the only place `AttendanceAlertDecision.abandoned()` is reachable, and the scanner files
  the student under "not added" rather than moving on.
- The scanner counts every scan that did NOT reach the queue (`_notAdded`, keyed by code, cleared on
  a successful retry) and shows «{n} ما اتضافوش» directly above the "review N" button, opening the
  shared not-recorded sheet (`showAttendanceNotRecordedSheet`, extended from the partial-save sheet
  — extend it, never fork a second one). **A short save must be visible at the door, not a week
  later.**
- That chip's warning colour is a **theme token**, not a hardcoded pair (2026-09-14). It shipped
  light-mode-only on a screen the app has a real dark theme for, and its light foreground
  `#B26A00` measured **3.90:1** — 12sp semibold needs 4.5:1. Now `#9A5C00` (**4.95:1**) with a
  dark-mode pair alongside. **A count of lost work is the last thing that may be hard to read**;
  when you add a semantic colour, add both modes and measure the contrast at the size it is
  actually rendered.

**A session's name lives in ONE place logically and EIGHT places physically, and the rename keeps
them in step.** Eight tables carry a denormalized copy of the session name, written once when the
row is created: `StudentSessionAssignment.SessionNameAtAssignment`,
`AttendanceRecord.SessionNameAtRecording` + `CrossSessionNameAtRecording`,
`StudentAbsenceCounter.LastAbsenceSessionNameAtRecording`, `PaymentPeriod.SessionNameAtGeneration`,
`PaymentTransaction.SessionNameAtCollection`, `StudentDeparture.SessionNameAtDeparture`,
`PaymentForgiveness.SessionNameAtForgiveness`. They exist for ONE reason (BR-ATT-005): sessions are
HARD-deleted (§4.3), so a history row must keep a readable name after its session is gone.

**Read the stored column directly. Do NOT resolve the live row.** `ISessionRepo
.PropagateSessionNameAsync(teacherId, sessionId, newName)` rewrites all eight with eight set-based
`ExecuteUpdateAsync` statements, called from the ONE rename in the codebase
(`SessionService.UpdateSessionAsync`, guarded on the name actually changing) — so the stored copy
never goes stale while the session exists, and on hard delete `SessionId` is NULLed and nothing
writes the row again, which is exactly the case the copies are for.

- **MUST BE LAST IN THE RENAME TRANSACTION.** `ExecuteUpdate` bypasses the change tracker, and that
  same method runs `RegenerateOccurrencesAsync` and the re-pricing, which load and SAVE
  `AttendanceRecords` / `PaymentPeriods` on the same context. A row still tracked with the old name
  and saved afterwards writes it straight back. Anything new that calls `SaveChangesAsync` in that
  method goes ABOVE the propagation.
- **`PaymentTransactions` needs `IgnoreQueryFilters()`** — it carries a global `!IsDeleted` filter
  and a refunded/corrected collection is still read (the refund history joins through it).
- Five of the eight are index seeks (`IX_AR_TeacherId_SessionId_OccurrenceDate`,
  `(TeacherId, SessionId, CollectedAt)`, `(TeacherId, SessionId, PaymentStatus)`,
  `(SessionId, IsActive)`, `(TeacherId, SessionId)`); `AttendanceRecord.CrossSessionId` and
  `StudentAbsenceCounter.LastAbsenceSessionId` have no index and scan — teacher-scoped, on an
  operation that happens a handful of times per session.
- **Adding a ninth session-name column means adding a statement to that repo method.** Nothing else
  can catch it: every read site is a plain column read by design, so a column that is never
  propagated simply shows a stale name forever. `scripts/check-session-name-propagation.sh` runs in
  CI beside the timezone gate and fails when (a) a `session.SessionName =` write is not paired with
  a propagate call in the same file, or (b) the repo method stops writing one of the eight.

**History — why the live-read design was tried and removed (do not reintroduce it).** A rename used
to write only `Sessions.SessionName`, so students assigned either side of one listed the SAME class
under two names: 90 of session 78's 126 students read «المجموعة» while 36 read «مجموعه الساعه ١٠».
`/api/v1/payments/students` served «المجموعة» and «مجموع الحامول» — names no session had had for
months — and it grows then sticks: **0 stale rows in July, 8 in August, 50 in September, 118 in
October and every month after**, because the transaction leg self-heals (the next payment stamps
the new name) while the period leg never does (periods are pre-generated one per month to the
session's end date, §7.4). The first fix resolved the live row at every display site. It was
correct and **expensive in daily operation**: a single-row query per mark on the attendance path
(~48 per 120-student class), a correlated `Sessions` subquery per counter row *inside* the
`ROW_NUMBER()` window on every roster page, an extra round-trip on five list endpoints, and a
two-subquery `COALESCE` per student on the payment tabs — all to compensate for an event that
happens a handful of times in a session's life. Propagation is the cheap half of the same
guarantee. The renamed properties (`…At<Moment>`) are kept: the compiler found all 78 call sites
during the first attempt, and the names still tell a reader when the value was written.

**Deliberately left as live reads:** `PaymentRepo`'s `.Include(p => p.Session)` +
`d.Session != null ? … : d.SessionNameAtDeparture` (3 sites), `PaymentService.DisplaySessionName`,
and `PaymentScreenService.ResolveSessionName` (2 sites). These predate the whole episode, are
already deployed, cost one join on a query that is loading the row anyway, and act as a free safety
net if a propagation is ever missed. Redundant, not wrong.

**The backlog is repaired by a JOB, not a migration** — Hangfire recurring `session-name-reconcile`
(03:30 Africa/Cairo, after the auto-absent sweep and the recycle-bin purge) →
`ISessionRepo.ReconcileStoredSessionNamesAsync`, the same eight columns repaired platform-wide in
autocommitting batches of 2000, capped at 50 batches per table per run, resuming the next night if
it does not finish. Guarded on the name actually differing, so a steady-state run writes nothing.

**The job is configured, clamped and killable** (`SessionNameReconcileOptions`, section
`SessionNameReconcile`, added 2026-09-14 — §6.5). `SessionNameReconcile__Enabled` /
`__BatchSize` / `__MaxBatchesPerTable` / `__CronExpression` are App Service settings, so a job
that rewrites eight tables platform-wide — two of them the hottest write paths here — can be
stopped in one settings save, with no redeploy and no DDL. `Enabled` is checked before any
statement runs. **The UPPER clamps are the load-bearing half**: `BatchSize` is clamped to
`[1, 5000]` and `MaxBatchesPerTable` to `[1, 1000]` because the failure mode is a typo in a
settings box, and a batch above SQL Server's ~5000-lock escalation threshold is exactly the
`AttendanceRecords` table lock this was made a job to avoid (next paragraph). Defaults reproduce
the previous hard-coded constants exactly, so the options class is a no-op until someone sets
something. `SessionNameReconcileJob` is also registered in DI now — Hangfire's activator could
construct it either way; it belongs with its siblings.

**Why not a migration.** `azure/sql-action` applies migrations BEFORE `az webapp deploy`, so the
CURRENTLY DEPLOYED app is live and teachers are marking attendance while they run. A repair of
unbounded size there does two bad things: it exceeds SQL Server's ~5000-lock escalation threshold
and holds a TABLE lock on `AttendanceRecords` until the migration commits (every attendance write
blocked), and its total runtime counts against one command timeout, so a slow repair extends or
fails the deploy. One rename makes a whole session's history stale at once (90 of 126 students ×
~50 records), so both were reachable. **Never put a data repair of unbounded size in a migration —
this codebase deploys schema before code, with the old app still serving.** Left running, the job
is also the net that catches drift if a ninth session-name column is ever added and not propagated.

**Status headcounts: two populations, never the loaded page.** The list response carried
`assigned_count` / `not_assigned_count` / `hold_count` and NO present/absent/unmarked totals, so
`AttendanceOccurrenceStudentsApiDto` counted the ten rows it was handed. The filter badges read
0 marked / 10 unmarked against a real 402 / 145, and — worse — the "Attendance saved" screen's
Present / Remaining / Hold cards used the same page-derived summary, drifted by in-memory
adjustment against a `clamp(0, …)`: after 113 scans **"Remaining" read 0 with six students still
unmarked**. It told the tutor he was finished.
- `present_count` / `absent_count` / `unmarked_count` describe the WHOLE list and label the chips,
  which open exactly that list. `assigned_present_count` / `…_absent_` / `…_hold_` / `…_unmarked_`
  describe THIS session's own register and drive the progress line + the saved screen. Both are
  measured on the searched set BEFORE any chip filter, like `assigned_count`.
- **The two are not interchangeable.** His 19:00 list holds 547 people (119 his, 428 who may visit
  from the 10:00/12:00/14:00 classes); on 12 Sep the list read 402 marked of 547 while his register
  was 91 of 119, because 311 of those marks were made in the three earlier classes. "402 of 547" as
  progress answers a question nobody asked.
- All nine numbers come from ONE grouped query — `GroupBy(IsMine, RecordStatus)` — which also
  replaces the four `CountAsync` calls this method used to make, so it is cheaper than what it
  succeeds. It has no `Count(predicate)` at all, so BUG-16 cannot apply. The row projection carries
  a nullable `RecordStatus` SCALAR rather than a sub-object: EF re-inlines a projected member per
  reference, and as an object the tally emitted **fourteen** correlated subqueries instead of one.
  Both queries were verified with `ToQueryString()` against the real model before shipping.
- Additive both ways: an older app ignores the fields; a newer app against an older server reads
  zeros and keeps the page derivation. The all-zero fallback guard now also checks
  `unmarkedCount`, or a genuine "0 present, 0 absent, 547 unmarked" was thrown away as "nothing
  sent". `hold_count` alone must never satisfy that guard.
- The offline slice computes both the same way (search → counts → chip filter → page), so offline
  is now exactly as accurate as online. The EXAM roster had the identical defect
  (`teacher_exam_take_attendance_cubit` counted its loaded pages) and now reads
  `presentCount`/`absentCount`/`unmarkedCount` off `ExamSessionRosterDto`; an exam occurrence has
  no linked visitors, so its register and list are the same population.
- App side: `TeacherAttendanceProgressLine` puts «اتسجّل ٩١ من ١١٩ · فاضل ٢٨» and a thin bar on the
  screen where the marking happens — one atomic status sentence in a `liveRegion`, never a bare
  number. Marks are adjusted in memory against BOTH summaries via `_adjustBothSummaries`, routed by
  `isAssignedToSession`; `TeacherAttendanceQueuedScan` carries that flag from the moment the scan
  resolved, because a scanned visitor is usually not on the loaded page to ask.
- **CORRECTION to `linked_absent_count` (2026-09-14).** Its doc comment used to claim a visitor
  "can only be Present or Held here — a cross-session mark is forced to `CrossSessionPresent`, so
  absent is unreachable for them". **That is wrong, and the number is routinely non-zero.** A
  row's status is resolved over the whole equivalent-slot occurrence set (this session plus every
  linked session sharing the `(WeekStartDate, DayPositionIndex)` slot), so a visitor marked Absent
  in THEIR OWN class — by their tutor, or by the nightly auto-absent sweep (§7.2) — surfaces on
  this roster carrying that Absent. The forcing rule only governs a PRESENT mark made here.
  So `linked_absent_count` counts **visitors absent from their own class**; it is NEVER "absent
  from this class", which they were never obliged to attend. That is exactly why the app offers a
  "from other classes" chip on the Present and Hold lists and **deliberately offers none on the
  Absent list** — the number is real, the sentence it would imply is not. Do not "add the missing
  chip".



**A lookup is a question, not a filter (app, 2026-09-14).** `lookupStudentByCode` used to
`emit(searchQuery: <the scanned code>)` and call `load(force: true)`, and **nothing ever cleared
that field**; `load()` reads `state.searchQuery` on every call. So from the first scan that missed
the in-memory fast path onwards, the whole cubit — roster, chips, `summary`, `registerSummary`,
`assignedCount` — described ONE student, and stayed that way through submit. `_submitQueuedScans`
hands `registerSummary` straight to the "Attendance saved" screen with no reload in between, so
that is the number the tutor reads.

**This is NOT the cause of «خدت الغياب وظاهر ١١٣ طالب وهما ١٢٠» — do not re-attribute it.** That
complaint was scans genuinely never queued, because closing the before-you-mark alert discarded
them (BUG-22), and it is already fixed. Scanning itself worked and the marks saved. The scoping
defect above reaches exactly ONE surface, the saved screen's numbers, and in the shipped build its
effect was masked: `summary` was page-derived from ten rows anyway, so a one-row search base was
the same class of wrong, not a new one. It is fixed here because the server totals only help if
nothing re-scopes them afterwards — a guard on the new numbers, not a repair of an old bug. The
lookup is
now one direct repository query that emits no list state (the exam cubit's has always worked this
way — it was the outlier). The zero-network fast path over rows already in memory stays.
Belt-and-braces on top: **only a load with NO search term may replace `registerSummary` /
`registerAssignedCount`.** The server measures every headcount on the SEARCH-filtered set on
purpose (so a chip cannot renumber itself when selected), which is right for the chips and wrong
for the progress line and the saved screen — searching one already-marked student would otherwise
turn the bar full green and announce «خلصت المجموعة» in a `liveRegion`. Marks made during a search
still move both summaries in memory, so the bar stays live. Same split applied to the exam cubit.

**`hold_count` was never parsed.** The roster response sends `hold_count`; the app looked for
`holdCount`/`heldCount`/`held`/`totalHeld`. Harmless while the all-zero fallback recomputed the
summary from the page — but once `present_count`/`absent_count`/`unmarked_count` started arriving
the fallback stopped firing and the hold count pinned at 0, so the filter menu's **Marked** total
(present + absent + hold) was smaller than the list that chip opens. It is now read, **gated on the
response also carrying the other three**: an OLDER server sends `hold_count` and nothing else, and
reading it there would put a non-zero number in an otherwise empty summary and defeat the fallback.
All ten snake_case keys on that DTO were cross-checked against the parser; this was the only miss.

**A scan whose student the roster never resolved has UNKNOWN membership.**
`TeacherAttendanceQueuedScan.isAssignedToSession` is `bool?` and defaults to **null**, and only
`true` moves the register. Membership-LINKED students are not the unknown case — the roster query
is `sessionId OR linkedIds.Contains(...)`, so a visitor resolves and arrives as `false`; only a QR
payload the search could not match is null. The two errors are not symmetrical: counting a
non-member in can make the register read finished while a real student is unmarked (the failure the
progress line exists to prevent), while leaving a real member out only reads one short, which the
tutor can see. Never collapse `null` back to `true`.


**Present / Remaining / Hold are SERVER lists, scoped to the card that opened them (2026-09-14).**
The three cards on "Attended students" (`teacher_attendance_attended_result_view`) open
`teacher_attendance_roster_view`. That screen used to call `loadRosterForKind`, which downloaded
the WHOLE roster ten rows at a time until it ran out (`fetchAllPaginatedEitherItems`, `maxPages:
100`) and then filtered in Dart — 41 sequential round-trips for 402 marked students, each running
the full roster query, and for Hold it downloaded every marked student to show six. Past **1000
students it silently truncated**: the loop stopped and `totalCount: filtered.length` agreed with
the short list, so nothing on screen disagreed.

- **`AttendanceStudentListRequest.Status`** (nullable) filters the existing roster query to one
  status. `Present` matches `CrossSessionPresent` too, expressed as
  `PresentStatuses.Contains(r.RecordStatus)` — `Contains`, not `a == x || a == y`, because the
  status is a correlated subquery in the projection and EF re-inlines a projected member per
  REFERENCE, so the OR form emitted it twice per row. It must stay the literal twin of the tally's
  `IsPresent`: they are the count and the list of the same thing (BUG-17 / BUG-23).
- **"Remaining" is not a status** — it is `UnmarkedOnly`, and it is ALWAYS scoped to the tutor's own
  register. A visitor who was never marked is not work he owes: on one live class, list-wide
  "remaining" would read 426 for a class of 119 because 398 visitors simply did not come.
- **Present and Hold default to the register and offer a visitors chip.** A visitor CAN reach both:
  a cross-session mark is forced to `CrossSessionPresent` (`AttendanceService` line ~694), and the
  Hold branch writes against THIS session with `IsCrossSession = false` and only checks the student
  has *an* assignment. Marking a visitor absent HERE is impossible — it silently records
  `CrossSessionPresent`, because they were never obliged to this class. **That does NOT make
  `linked_absent_count` zero**: a visitor absent in their OWN class arrives carrying that Absent
  through the equivalent-slot resolution — see the correction at the end of this section. The
  Absent list therefore has no visitors chip, on purpose.
- **`linked_present_count` / `linked_absent_count` / `linked_hold_count` / `linked_unmarked_count`**
  are sent as their own set. The grouped query already produces both halves, so this costs nothing
  and means **no screen subtracts one server number from another** to label a chip — a subtraction
  is exact today and silently wrong the first time the two are measured on different sets.
- **`teacher_attendance_attended_result_view` builds its own cubit and loads**, so the summary its
  caller passes is only the first frame; `displaySummary` decides what the cards show thereafter.
  It read `state.summary` (the whole linked family) — "Present 402" for a class of 119 — and now
  reads `state.registerSummary`. **A screen that re-loads must re-derive from the same population
  its caller intended, or the argument is decoration.**


**Take-attendance chip filters.** The app has always sent `AssignedOnly` / `LinkedOnly` /
`MarkedOnly`; `AttendanceStudentListRequest` bound none of them, so online every chip except
"unmarked" returned the whole list while the offline slice filtered client-side — the same chip,
two answers. All three are bound now. The chip LABELS (`assigned_count` / `not_assigned_count` /
`hold_count`) are measured on the search-filtered set **before** any chip filter, so selecting a
chip never renumbers the chips; `totalCount` and the page use the fully filtered set. The offline
slice was reordered to match (search → counts → chip filter → page). Note `holdCount` on the wire is
ignored by the app (`AttendanceOccurrenceStudentsApiDto` derives the summary from the page).

**Offline payment status is not one truth, and must say so.** Tracking holds only the aggregates the
server computed — no per-student rows — so an offline read can only ever be the last download, and
it used to be served with nothing saying that. A tutor who had just collected saw "paid" on the
collect screen (which overlays the outbox) and unpaid in the tracking counts: «ياسين دا انا دفعته …
وظاهر عنده أنه دافع بس عندي لا». No money was ever lost (his ledger is one clean transaction per
month). The fix is honesty, never client-side money arithmetic: tracking carries
`servedFromCacheAt` + `pendingCollectCount` and renders "as of {time} — {n} collections aren't in
these figures yet". **Do not recompute `statusBreakdown` / collected / remaining on the device** —
a prorated or multi-month collect makes that wrong in ways a timestamp never is. Two related fixes:
the attendance roster's `paymentInfo` is suppressed for any student with a live collect in the
outbox (a stale unpaid alert is what opened the dialog that dropped scans), and the offline collect
search matches `studentCode` — it compared the numeric `id`, so typing "86C" offline found nobody
while scanning the same code worked.

---

### 7.11 Admin activation records only what the admin STATES (2026-09-14)

`POST /api/admin/subscriptions/activate` (and `-managerial` / `-managerial-plus`) bypasses payment:
the money is arranged outside the app, so the server knows only that *someone decided to switch
this teacher on*. A free trial, a goodwill month and a paid month are indistinguishable from here.

- **The price is NEVER computed at activation.** `ActivateCoreAsync` briefly priced the period
  through `SubscriptionPricing.MonthlyValueEGP` and stored the result, reasoning that the platform
  otherwise has no memory of what a period was worth. It was wrong in the direction that matters:
  a teacher granted a free month then read, **in their own subscription history**, money they had
  never paid. A figure nobody stated is not a better record than no figure — it is a false one.
  See §8 BUG-27. `ComputeRenewalPriceAsync` / `SubscriptionPricing` remain the source for the
  teacher's renewal QUOTE; that is a different question and is untouched.
- **`AmountPaidEGP` on `AdminActivateRequest` / `AdminActivateManagerialRequest`** is `decimal?`,
  exactly mirroring `AdminExtendRequest.AmountPaidEGP`: **null means "not stated", which is NOT
  the same as zero.** Omitted stores `0m` — the historical value — so an admin client that never
  sends the field behaves byte-for-byte as before.
- **A stored zero must never render as a price.** `TeacherSubscription.AmountPaidEGP` is
  `decimal NOT NULL` (and stays that way — **no migration, no schema change**), and a real payment
  is never zero, so a stored `0` means "nobody wrote a figure here". `SubscriptionHistoryItemDto
  .AmountPaidEGP` is therefore `decimal?` and `SubscriptionService.GetHistoryPagedAsync` maps
  `0m → null`. **A null cannot be formatted into a price by accident; a zero can.** The JSON key is
  still always present (it serializes as `null`, not omitted) — only its type widened from number
  to number-or-null.
- Scope check before changing this: `GET /api/subscription/history` is the ONLY surface that shows
  this column to a teacher. `CurrentSubscriptionDto` does not carry it, and
  `CurrentSubscriptionStatusProjection.AmountPaidEGP` is populated but read by nothing. The admin
  console's `RecordedPaidEGP` / `RecordedAmountPaidEGP` still sum the raw column and are a **floor
  on recorded revenue, never a total of it** — rows with no stated amount contribute 0, which is
  why `ValueBasis` stays `"TodayPrices"` and the two are reported unmixed.

---

### 7.12 Books & fees — «مذكرات ومصاريف» (completed 2026-09-14)

A thing the tutor sells ONCE — a مذكرة, a رحلة, a uniform, an exam fee — to a chosen set of students,
then chases. The backend shipped in Apr 2026 (`254c5d6`) and its tables have been live since the
baseline migration, but **no client ever called it**, so it was never exercised. Completed in full.

**THREE NAMESPACES, DELIBERATELY DIFFERENT. Do not unify them.**

| Layer | Value |
|---|---|
| DB `Models.Name` + `Permissions.Name` | `"Event-Based Payment"` · View/Create/Edit/Delete/CollectPayment/GenerateReports |
| Existing C# + routes | `PaymentEvent`, `EventStudentObligation`, `EventPaymentTransaction`, `api/eventpayment/*` |
| NEW identifiers | `Extras` (`kind=extras`, `ShowExtrasOnAttendanceScreen`, `ExtrasItemTotals`…) |
| User-visible copy | «مذكرات ومصاريف» / "Books & fees" |

The DB module name and the six permission strings are **live authorization keys** —
`PermissionHandler` matches `snapshot.Modules.Contains(module)` and
`snapshot.Permissions.Contains($"{Module}.{Permission}")`. Renaming either revokes every teacher's
and assistant's access. The rename was therefore **display-only**: 13 resx label values + the admin
UI's `feature-labels.ts`. Prod-verified 2026-09-14: module id 5, all six permission rows present,
**61 users hold them (50 live assistants with `Edit`, across 25 tutors)**.

**Subscriber-only.** `ModuleQuotaKeys.Events` is **0**, so `CanCreateAsync` short-circuits without
counting (like Assistants/Groups/Triggers). The create gate returns the distinct key
`ExtrasRequireSubscription`, and `GET api/subscription/status` carries
**`features.extrasAllowed`** so the app renders a paywall card instead of a bare 403. Clients gate on
that field ONLY and fail OPEN on absence — never on `hasSubscription` plus their own plan reasoning.
Existing free-tier items keep working: the gate is on CREATE.

**ONE unified collections ledger, and `kind` MUST default to `fees`.**
`Queries/CollectionLedgerQueries.cs` is the single definition of the ledger's source set —
`PaymentTransactions` **`Concat`** `EventPaymentTransactions`. **`Concat`, never `Union`**: `Union`
dedupes, and two students paying the same amount at the same instant are distinct rows that must both
count. (`VideoAudienceQueries` uses `Union` for the opposite reason.) `kind` (`fees`|`extras`|`all`)
defaults to **`fees`** on `/collections` and `/collections/summary`, so every deployed build is
byte-identical; verified with `ToQueryString()` that the fee scope emits the same single-table SQL and
`ORDER BY CollectedAt DESC, Id DESC` as before. `OrderedLedgerRows` adds the `Kind` tiebreak ONLY
when the query is genuinely a union — on a single source it would emit a useless `(SELECT 1) DESC`
constant sort key on the hot fee path. Extras row ids are **prefixed `"extras-"`**; the row field is
**`PaymentKind`**, not `Kind`, because `AssistantWalletCollectionItemDto.Kind` already means the line
TYPE and both render on the merged wallet screen. `AmountTiers` comes back EMPTY under `kind=all` on
purpose — a fee tier is a per-month settlement slice, an extras tier a per-item amount. Withdrawals
are omitted under a kind filter: one bag, two kinds, not attributable.

**Writing extras as real `PaymentTransaction` rows was rejected**, and must stay rejected. Seven
aggregates would silently change meaning — `GetCashCollectedInRangeAsync` feeds
**`CenterRevenueService`** (money billed to centers); `GetCashCollectedBreakdownAsync` and
`GetCollectionAmountTiersAsync` group `PaymentTransactionAllocation` rows an extras payment has none
of; the per-session cards are `SessionId`-keyed; `UpdatePaymentCounterAsync` advances PERIOD counts;
and worst, `PerOpIdempotentPaymentTransport.reconcile` matches a queued FEE op by same-day+amount, so
an extras row among them could reconcile a real fee collection away.

**The wallet is one physical bag.** Extras cash has ALWAYS credited
`AssistantWallet.CurrentBalance`, so the collector's ledger rows never summed to the balance printed
beside them. `GetAssistantWalletScreenAsync` now folds extras collections AND extras refunds into the
signed stream that reconstructs the held balance and anchors `HeldSinceAt` — that stream must include
both kinds or the anchor lands on the wrong event. `WalletBalance` / `TotalCollectedAllTime` /
`HeldSinceAt` are **never** filtered by `kind`; the wallet endpoint's `kind` therefore defaults to
**`all`**, unlike the ledger's. `TotalCashCollected`/`TotalRefunded` DO now include extras — that is
the fix, not a regression, since those two exist to explain the balance.

**Everything else is additive.** `Summary.CollectedTotal` keeps BOTH its invariants
(`= ThisMonth + Previous + Advance`, and ties to `CollectedByAssistant.TotalCollected`);
`CollectedExtras` / `CollectedCashAllSources` are new, with the twin invariant
`collectedCashAllSources == collectedByAssistant.totalCollectedAllSources`. `ExpectedRevenue` /
`RemainingAmount` / `statusBreakdown` stay **strictly monthly fees** — they reconcile to
`TotalStudents` by construction, and an unpaid مذكرة surfaces in Books & fees, never in the fees
"unpaid" bucket. The yearly view is a per-month installment grid and is structurally fee-only.

**Audience: `PaymentEventScopes`, and `IndividualStudents` is never a scope row.** The old
`TargetScopeIds` held a comma-joined list of RESOLVED STUDENT IDS, so "by session / by group" was
structurally unanswerable, and a multi-scope create collapsed `TargetScopeType` to
`IndividualStudents`. The new table follows `OnlineExamScope` — composite tenant FK
`(PaymentEventId, TeacherId)` declared ONCE, plus an explicitly-declared Teacher FK (leaving it to
convention produced **CASCADE**), and **ONE** check constraint folding both shape rules because
`AllStudents` carries no target. The unique index needs
**`.HasFilter((string?)null)`** or EF Core 10 silently disables it. Individually-targeted students are
already recorded by their obligations, and a `TeacherStudentId` FK here would need purge handling the
CHECK constraint forbids — the same reason `VideoScope`'s individual branch was removed.
`TargetScopeType`/`TargetScopeIds` are still written, for rollback safety.

**`SessionService` MUST delete these scope rows on session AND group hard-delete** — the two new
calls sit beside the existing `VideoScope`/`VideoUnitScope`/`OnlineExamScope` ones. Both target FKs
are `NoAction` and the CHECK forbids nulling them, so a surviving row fails the delete with a 409.
Adding any new scope-style table means adding it there too.

**Auto-include is per-item, once per CALL, at SIX sites.** `PaymentEvent.AutoIncludeNewStudents`
(default off) decides whether a later joiner gets an obligation automatically; when off, the item
screen shows "N new students · add them?". `MaterializeAutoIncludeForSessionAsync` /
`…ForNewStudentsAsync` both take COLLECTIONS and run **inside the caller's transaction** — not
post-commit best-effort, because an obligation that silently fails to appear is money the tutor never
learns they are owed. Never move this into the per-student `OnStudentAssignedToSessionAsync` hook: a
300-student bulk assign with 5 live items would be 300 scope queries and 1,500 inserts under one set
of locks. The six sites: `SessionService.AssignStudents`, `SessionService` reassign, student create
(with session), student create (**no** session — the only path an `AllStudents` item reaches them by),
**student EDIT move**, and **bulk import** (grouped BY SESSION, so 400 students across 6 classes is 6
queries). The last two are the easy ones to miss.

**Exempt is `IsExempt`, NOT a fifth `PaymentStatus`.** That enum is shared with `PaymentPeriod` and
`PaymentTransaction.PaymentTransactionStatus`, and `!= Paid` predicates are everywhere in the FEE
module — a fifth value would sweep "not taking it" into "owes money" across
`GetStudentPaymentStatusCountsAsync`, `GetPaymentInfoForAttendanceBatchAsync` and more. Payment status
and exemption are **orthogonal**: an exempt obligation is `Unpaid` AND `IsExempt`, and "unpaid" means
`!IsExempt && PaymentStatus == Unpaid`. Exempting is **blocked (409) once `AmountPaid > 0`** — refund
first; that is what keeps `Σ AmountPaid == Σ non-deleted payments` true in every state.

**Money corrections live on `EventPaymentEditLogs`, a SEPARATE table.** Not `PaymentEditLogs`:
`GetCollectorRefundsInRangeAsync` *derives* the fee refund ledger from that table, so extras rows
there would force every existing reader to prove it excludes them — the same "a shared identifier is
dangerous" argument as `PaymentStatus`. Every subject reference is a bare nullable long with **no
FK**, and the student/item names are denormalized, so a refund row still renders after a purge.
`ChargedToUserId` encodes the attribution at WRITE time (for extras always the ORIGINAL collector,
whose figure the correction corrects), which keeps the ledger derivation a plain equality instead of
the two-branch CASE the fee side needs. `CollectedAt` carries the reversed cash's original instant so
`AdjustCollectorWalletAsync`'s reset-aware rule works without re-reading a deleted transaction.
Refund and edit-amount are **tutor-only** (`roleOnly`), matching `DELETE api/payment/transactions/{id}`
and BR-PAY-002; exempt sits under `Edit` because it moves no cash.

**Wallet helpers are SHARED, not re-implemented.** `IPaymentService.CreditCollectorWalletAsync` /
`AdjustCollectorWalletAsync` (promoted from private) bring the `MaxConcurrencyRetries` RowVersion
retry loop, lazy wallet creation for a **CenterAssistant** collector (whose extras cash previously
reached no wallet at all), the teacher-owner no-op, and the reset-aware refund rule. The old inline
mutation had none of them.

**Tracking: ONE grouped query feeds the header AND both breakdowns.** `GetExtrasTallyAsync` groups
`(current session, group, bucket)` where the bucket is a projected typed byte via `CASE` — never nine
`Count(predicate)` aggregates, which is how BUG-16 becomes structurally impossible. The header,
`bySession` and `byGroup` are folded from that one result, so a breakdown cannot drift from the total
above it. Sessions are the student's **CURRENT** class (the same column the audience resolves
through, §7.8), and a student with no class gets a row with `id: null` — render it, or the rows stop
summing to the header. `.Where(o => o.TeacherStudent != null)` before every count (BUG-8/BUG-17).
Roster chip counts are measured on the SEARCHED set **before** the chip filter, share one bucket
definition with the list, and order exact-`StudentCode`-first with the obligation id as a unique
tiebreaker.

**The cached aggregate columns are a CACHE that nothing reads.** `PaymentEvent.TotalStudents` /
`TotalExpectedRevenue` / `TotalCollectedRevenue` are still written so they don't rot, but every
response derives from the obligation rows — the item list via one grouped query keyed on the page's
ids. Three separate write paths used to clobber them independently. This also finally populates
`EventDto.PaidStudents`/`PartiallyPaidStudents`/`UnpaidStudents`, declared from day one and **never
set** (every client read 0). Exempt students are excluded from the headcount and from expected
revenue; `ExemptAmount` is reported so "expected" stays explainable.

**Two-level attendance switch.** `TeacherConfiguration.ShowExtrasOnAttendanceScreen` (`bool?`, read
`?? true`) is the teacher-level gate; each item also carries `CollectDuringAttendance`, whose DB
default is **`0`** so items predating the feature never start interrupting attendance on deploy (the
create path always sends `true`). `GET api/eventpayment/debts` returns `showExtrasInfo: false` with an
empty map when off — "off" and "nobody owes anything" must not look the same (the `ShowPaymentInfo`
precedent). Student/parent visibility: `StudentVisibilityExtras` / `ParentVisibilityExtras`, both
defaulting **FALSE** unlike their siblings, read through the shared `PaymentViewerType` gate so the
two audiences cannot drift. Note `StudentVisibilityExtras` is the app's FIRST student-visibility
switch — the other four `studentVisibility*` flags have no cubit setter and no UI.

**No session column on `EventPaymentTransaction`, deliberately.** A ninth session-name column would
have to join `ISessionRepo.PropagateSessionNameAsync` and its CI gate (§7.10). "By session" groups on
`TeacherStudents.SessionId` instead.

**Export stays deferred** application-wide. `PaymentReportExportService.ExportEventReportAsync` still
returns a placeholder string, so **no export route is exposed** — a button that downloads a corrupt
"PDF" is worse than no button.

**Removal is tutor-only, and that is deliberately STRICTER than the platform's own precedent.**
`POST api/payment/departure/confirm` is gated on `Payment.Collect`, so an assistant can confirm a
student LEAVING — which pays real cash out of their own wallet — while `StudentIdsToRemove` on an
extras item, which moves no money at all and is refused outright once `AmountPaid > 0`, stays
`roleOnly ["Teacher","SuperAdmin"]`. Decided 2026-09-15 in favour of the written rule (BR-EVT-003)
over the precedent. Zero practical impact: no assistant has ever exercised the path. **If this is
ever revisited, the argument for widening it is the departure precedent, not convenience** — and
the 50 live assistants holding `Edit` are what the permission catalogue already implies.

**Undoing a collect is a PAYMENT action, not an obligation action.** A student may have paid in two
partials, so "refund" with no payment named would have to guess which. `GET
api/eventpayment/items/{id}/students/{sid}/payments` lists them (gated `View`, also satisfied by
`CollectPayment` — reading who paid what is not a money action), and each row carries its own
refund and correct-the-amount, both tutor-only. This mirrors `TeacherPaymentEditPaymentsSheet` on
the fees side exactly, so a tutor looks in ONE place for both kinds of money. Already-refunded
payments are ABSENT from the list (the transaction query filter), so none can be reversed twice.
**`IsEdited` / `OriginalAmount` are DERIVED** from `EventPaymentEditLogs` via `MIN(Id)` per payment
— never denormalized onto the transaction, and the EARLIEST log on purpose: after a second
correction the tutor needs the original, not the middle value.

**The audience is changed from the TRACKING screen, and the form says so.** The create/edit form
renders the audience read-only with a lock, the reason, and «ضيف أو شيل طلاب» as a real button —
prose alone reads as "this can never be changed". Adding is `POST`-free (`studentIdsToAdd` on the
update) and is preceded by a confirm NAMING the behaviour, because the picker it opens is the same
screen as the audience picker and a tutor would otherwise assume an unticked student gets removed.
**The form pops a typed `ExtrasFormOutcome` (`cancelled` | `saved` | `openStudents`) and the LIST
navigates** — a screen cannot pop itself and then use its own `BuildContext`; the `mounted` guard
after a pop is always false, so the button would have gone nowhere, silently.

**Delete vs close (added with the UI).** `DELETE api/eventpayment/events/{id}` answers **409
`ExtrasItemHasPaymentsCannotDelete`** once any money has been collected and not refunded, and the
message names closing as the remedy. It must: the transactions survive a delete (the FK is SET NULL)
and still appear in the collections ledger, so deleting would leave cash on screen with nothing left
to explain it. A delete that IS allowed removes the money-free obligations in one set-based statement
(the FK is NoAction while the delete is soft, so they would linger forever) and stamps
`DeletedByUserId`; paid obligations stay as the attribution for a surviving transaction and the
item's query filter hides them. `PUT api/eventpayment/items/{id}/closed` {`closed`} is the escape
hatch — reversible, idempotent, tutor-only like DELETE, and it stops auto-include too. Explicit state
rather than a toggle route, so a retried request lands where the caller intended.

**The item list filters on the OBLIGATION ROWS, never the cached columns.** `completionStatus`
accepts `Open` | `FullyCollected` | `PartiallyCollected` | `NotStarted`, each an EXISTS over
obligations (live student, not exempt, `AmountPaid < AmountDue`). `Open` exists as a SERVER value on
purpose: a client narrowing only the loaded page would hide every unsettled item past page 1 behind
a chip claiming otherwise. The ordering carries `ThenByDescending(Id)` because `EventDate` is a
calendar day and ties are routine.

**`ScopeSummary` is how a row says who it is for.** Built from `PaymentEventScopes` in ONE query
keyed on the page's ids, with target names resolved LIVE (the scope row dies with its session, so
this is not a §7.10 history column). `type` is a STRING — `sessions|groups|students|all|mixed` —
because "mixed" is not a scope type: one item can target two classes and a group. `all` absorbs
anything narrower. An item with NO scope rows reports its legacy `TargetScopeType`, so individually
targeted and pre-scope-table items still read truthfully.

**`UpdateEventDto` gained `EventDate` / `AutoIncludeNewStudents` / `CollectDuringAttendance`, all
NULLABLE.** Omitted means UNCHANGED on every field on that DTO — an old client must not flip a
switch it does not know about (BUG-20). The AUDIENCE is not editable: rewriting targeting rules
retroactively would create and destroy obligations money may already be attached to, so students are
added and removed individually (`StudentIdsToAdd` / `StudentIdsToRemove`, removal tutor-only) from
the tracking screen where the tutor can see who they are. The app's edit form renders the audience
read-only and says so.

**Two numbers on the Payments tab are NOT month-scoped**, alone on that response:
`summary.ExtrasOpenItemCount` / `ExtrasOutstanding`. An item is a one-off sale, not an installment,
so "what is still owed on books & fees" has one answer and paging back to July must not change it.
Closed items are excluded — their arrears are not collectable and the card exists to say what is
left to do. One grouped statement; the item count is the number of groups, so the two figures can
never describe different sets.

**`GET /collections/summary` takes `kind` too, and the scope rules are not symmetrical.** Money and
activity narrow; the paid/partial/prorated/unpaid counts do NOT — they are an obligation lens
anchored to a calendar month and extras settle no installment month. Under a `sessionId` filter the
extras half is always ZERO rather than wrong, because an extras payment carries no session (the same
reason "Collected by Sessions" stays fees-only). `StudentsPaidCount` is SUMMED across kinds, not
deduped: one student paying a fee and a مذكرة counts twice, which is accepted because deduping needs
a DISTINCT over the union of both tables and the default `fees` scope — every deployed build — is
byte-identical to what it replaced. `FeesTotal`/`ExtrasTotal` are reported ALWAYS, whatever the
scope, so no client subtracts one server number from another.

**App surfaces (Phase 2).** `lib/feature/teacher_module/extras/` — one card on the Payments tab
(locked card → paywall, driven by server `features.extrasAllowed`, fail-open when absent), an item
list, a create/edit form, and a tracking screen with three breakdowns over a paged roster. The scope
picker is the SHARED `TeacherExamTargetScopeView`, adapted by `ExtrasAudienceBuilder` — never forked.
The resolved audience count is labelled **"at least N"**: sessions and groups overlap and only the
server dedupes. Amount tiers are HIDDEN under `kind=all` (`showAmountTiers`): fee tiers are
per-month-installment and extras tiers per-item, so a merged "300 → 14" bucket would mean two things
at once. The ledger's kind badge is a STATIC badge — the segmented control is the interactive thing
— and only an extras row with an item id is tappable.

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
| BUG-12 | `ParentUserController` (all 10 endpoints) | Mass horizontal IDOR: the controller had NO `[Authorize]`/`[ModulePermission]` and injected no identity service, so `parentUserId`/`childId` were trusted straight from the route — any authenticated user (any role) could read/modify/delete ANY parent's profile, dashboard, and children. Fixed 2026-07-16 (commit `ece9fab`): identity is resolved ONLY from the JWT via `ResolveParentUserIdAsync()` (`User.Id` → active `ParentUser`); the `{parentUserId}` route segment is kept for wire-compat but IGNORED (0/null/wrong/mismatched all behave identically); every `childId` is scoped to the resolved parent inside the service (`GetActiveChildAsync(parentUserId, childId)`); class `[Authorize]` + per-endpoint `[ModulePermission(roles:["Parent"], roleOnly:true)]` added; `InitializeParentUser` forces `dto.UserId` from the JWT (registration still initializes parents server-side via `UserService`, unaffected). Mirrors `ParentAttendanceController`/`ParentPaymentController`. Generalizes §3.3 — never trust a route/body identity id (teacherId, parentUserId, childId); resolve from the token. |
| BUG-14 | Parent portal: a wrong student code was answered "request sent" and waited forever | `RequestAccessAsync` returned the neutral pending payload for a nonexistent `StudentCode` **without writing a row**, and the portal's `/pending` treated the resulting `state: "none"` as "still waiting", refreshing every 15s indefinitely. The parent believed they had asked; no teacher could ever see or approve anything; support could not tell the case apart from a slow teacher. Fixed 2026-09-11 — honest 404 + a metered enumeration budget, and `/pending` renders only a genuine `pending`. Full contract in §7.7. **Never make a discarded request answer `pending` again**, and never let a screen assert a state the API did not return. |
| BUG-13 | `MessagingController` auth commented + `TeacherController.GetTeachers` (`GET /api/teacher/list`) ungated | Two authorization holes closed 2026-07-16 (commit `000f009`). (a) MessagingController's class `[Authorize]` and the `send`/`history`/`resend` `[ModulePermission]` gates were commented out (this was §7.1 P0-A) → any authenticated caller could send manual messages, read history, and resend; restored verbatim — `"SendManual"`/`"ViewHistory"` are registered Messaging permissions (`DbInitializer`), `roleOnly:false` runs the `PermissionRequirement(module,permission)` check, and the tenant is still JWT-forced by `TenantScopeFilter`. (b) `GetTeachers` is documented Super-Admin-only but carried no role gate → any authenticated user could enumerate every teacher (name, code, phone, capacity, subscription); added `[ModulePermission(roles:["SuperAdmin"], roleOnly:true)]` (`roleOnly:true` → role-membership gate). Do not re-comment controller auth attributes or ship an admin-only endpoint without a role gate. |
| BUG-15 | `TeacherAttendanceQrScanView` scanned into the wrong day | The scanner builds its own roster cubit and was never handed `occurrenceDate`, so every scan wrote to the teacher's current local day whatever class day the register was showing. Opening an exam's class day and scanning marked the student present on the most recent class: the exam still read absent, a different day gained a Present nobody made, and the header ("Take attendance", no class, no date) gave no way to notice. Fixed 2026-09-11 — the day is threaded route → view → cubit (queued offline scans carry it too) and the header names class and day. Tapping a row was always correct; only the scanner was wrong, which is why it read as random. **Never let a sub-screen re-derive scoping context its caller already had, and never let a screen write to a day it does not name.** See §7.8. **Extended 2026-09-14 — the same defect, one screen further on**: the POST-SAVE chain (saved splash → attended result → the Present / Remaining / Hold status lists) each builds its own cubit and was never handed the day either, so after scanning from an exam screen the numbers described TODAY while the marks had gone to the exam's class day. `occurrenceDate` is now threaded through all of them, and **every screen that shows a day's numbers names that day**. The rule is not "the scanner needs the date" — it is that a cubit built by a sub-screen inherits NOTHING, so any scoping the caller had must be passed explicitly and then said out loud on screen. |
| BUG-16 | `COUNT(NULL)` from a predicate EF folded to a constant | `g.Count(x => x.SomePredicate)` in a GroupBy projection, where the predicate collapses to a compile-time constant `false` (a captured C# bool that is false for this call), is folded by EF into literal `COUNT(NULL)` — SQL Server rejects it outright (*8117, "Operand data type NULL is invalid for count operator"*) and the endpoint 500s. Data-dependent, so it survived review, unit tests and a `ToQueryString()` check that only ran the true branch; three live videos whose duration was still 0 returned 500 from `analytics/by-session`. Fixed 2026-09-11 by expressing "impossible" as an unreachable VALUE (`long.MaxValue` watch-seconds bar) instead of a bool, which keeps it a real column comparison and removes the division from SQL entirely. **When a captured flag gates a predicate inside a SQL aggregate, run `ToQueryString()` for BOTH values of the flag.** |
| BUG-17 | Tracking-view `TotalCount` counted students the list could not show | The count was taken on the obligations alone, before the projection joins `TeacherStudent` (soft-delete filtered) and drops purged students. Exam 69 session 81 reported 162 and could only render 161; "not graded" reported 86 and rendered 85, so paging asked for a page that did not exist. Fixed 2026-09-11 with the `.Where(o => o.TeacherStudent != null)` BUG-8 already requires, applied BEFORE the count. The audience helper's per-student branch had the same shape and now joins `TeacherStudents` like its siblings. **A count that labels a list must be taken on the same population the list projects through.** |

| BUG-18 | Every gated-file "download" in the app (`AppHelper.openUrl` on `/api/files/{id}`) | `GET /api/files/{fileId}` is `[Authorize]`, but the app handed its URL straight to `launchUrl(..., externalApplication)`, which sends **no Authorization header** — so the system browser/PDF viewer got **401** and the file never opened. Images were unaffected only because `CachedNetworkImage` was given the bearer explicitly (`authenticatedImageHeaders`). Shipped broken on the teacher video overview tab and the student video lesson screen. Fixed 2026-09-13: `GET /api/files/{fileId}/url` returns the SAS as JSON (`TryGetReadUrlAsync` already produced it; the original action only redirected it), and `GatedFileOpener` resolves through Dio with the bearer, then opens the SIGNED url. **Never let the HTTP client follow the gated endpoint's 302** — that forwards our JWT to `blob.core.windows.net`. Any new file surface must go through `GatedFileOpener`, never `openUrl` on a gated URL. |
| BUG-19 | `TeacherSessionAttendanceMonthCubit.load` cleared the unsaved edit buffer | The terminal success emit set `pendingEdits: const []` unconditionally, so a **pull-to-refresh or a month change silently threw away a teacher's unsaved marks**, and search had to be hidden from the Edit screen entirely to avoid a third way to lose them. Fixed 2026-09-13: edits are keyed on `(teacherStudentId, occurrenceId)` — stable across a refetch — and re-painted onto the reloaded rows (`_reapplyPendingEdits`), with month counts recomputed from each cell's SERVER status so a re-apply can never double-count. Search is now on BOTH modes; a month change abandons the buffer but asks first. **Do not re-add an unconditional `pendingEdits: const []` to a load path.** |

| BUG-20 | Editing an offline exam wiped its description | `ExamViewDto` never returned `Notes`, so the edit form opened the description blank — and `ExamService.UpdateExamAsync`'s STRUCTURAL branch then assigned `template.Notes` unconditionally, writing that blank back. Any edit that moved a date or changed the sessions silently destroyed the exam's notes. It looked random because the metadata-only branch (`ExamHomeworkService.UpdateTemplateAsync:1606`) has always had an `if (dto.Notes is not null)` guard, so renaming an exam preserved them. Fixed 2026-09-13 on all three legs: the GET returns `Notes`, the form hydrates it, and the structural branch guards like its sibling — **omitted = leave alone, empty string = clear**, so a still-deployed old build can no longer wipe anything. **Never assign an update field unconditionally when the client may legitimately omit it**, and check that the GET returns every field the PUT can write. |
| BUG-21 | Offline scan: a short student code resolved to "الطالب غير موجود" | The scanner searches, loads page 1 (10 rows) and requires an EXACT `studentCode` on it. The server ranks the exact match first; the offline snapshot slice did not, so a code that is a substring of many others fell off page 1 and a student standing in the room scanned as not found. Measured on one live roster: `8B` matched 16 codes and sat 12th; `8B`, `8A`, `3C` were all unreachable. It only ever happened offline, which is why it read as random. Fixed 2026-09-13 with `rankExactCodeFirst` shared by both offline slices, plus the same ranking added to the EXAM roster (`ExamHomeworkRepo.GetTrackingViewPagedAsync`), where it failed ONLINE too. **A list a scan resolves through must rank the exact code first on both sides of the connection.** See §7.10. |
| BUG-22 | Closing the "before you mark" alert discarded the scan, silently | The absence/unpaid prompt returned `TeacherAttendanceStatus?` and all seven call sites read `null` as "do nothing"; it was `barrierDismissible: true` with no cancel button. 41% of one live roster (222/547) raises this alert, so a stray tap at the classroom door is how a class of 120 saved as 113 with no error anywhere. Fixed 2026-09-13: non-nullable `AttendanceAlertDecision`, no barrier dismissal, closing resolves into the mark the tutor was making and is reported by name, and the scanner surfaces a "{n} not added" list. The server-forced absence confirmation (REQ-ATT-057/058) is the ONLY dismissal that records nothing, and it warns first. **Never let a prompt's dismissal be indistinguishable from "nothing happened".** See §7.10. |
| BUG-23 | Take-attendance counts described the loaded page, not the class | `AttendanceStudentListDto` returned no present/absent/unmarked totals, so the app counted the ten rows it was holding. The filter badges read 0 / 10 against a real 402 / 145, and the "Attendance saved" screen — same page-derived summary, drifted by in-memory adjustment against a `clamp(0, …)` — showed **Remaining 0 after 113 scans with six students still unmarked**, telling the tutor he had finished. The exam roster counted its loaded pages the same way. Fixed 2026-09-13: nine server-side totals in one grouped query, split into the whole LIST (labels the chips) and THIS session's REGISTER (progress + saved screen), because in a linked family 311 of the list's 402 marks belonged to three other classes. **A count that answers "am I done?" must be measured on the register, and never on the page that happens to be loaded.** See §7.10. |

| BUG-24 | `lookupStudentByCode` left the roster filtered to the scanned code | It set `state.searchQuery` and called `load(force: true)`; nothing ever cleared it, and `load()` reads that field every time. After the first scan that missed the in-memory fast path, the roster, the chips, `summary`, `registerSummary` and `assignedCount` all described ONE student, and stayed that way through submit. **It is NOT the cause of the 113-of-120 report — that was BUG-22, scans the alert discarded, and scanning itself worked.** The only surface it reaches is the saved screen's numbers, and in the shipped build that was already page-derived from ten rows, so the effect was masked. It matters now because server-side totals only help if nothing re-scopes them afterwards. Fixed 2026-09-14: the lookup is one direct repository query that emits no list state (mirroring the exam cubit, which was always right), and only a search-free load may replace the register counts. **A lookup is a question, not a filter — never let one re-scope the list it read from.** See §7.10. |
| BUG-25 | `hold_count` unread, so the "Marked" chip under-counted | The parser looked for `holdCount`/`heldCount`/`held`/`totalHeld` and never the wire key `hold_count`. Masked while the all-zero fallback recomputed the summary from the loaded page; once `present_count`/`absent_count`/`unmarked_count` shipped the fallback stopped firing and the hold count pinned at 0, so the filter menu's Marked total (present + absent + hold) was smaller than the list that chip opens. Fixed 2026-09-14 by reading it **only when the response also carries the other three** — an older server sends `hold_count` alone and reading it there would defeat the fallback, which is why it was excluded in the first place. **A wire key that is only sometimes meaningful needs a contract signal, not exclusion.** |
| BUG-26 | An unresolved scan was counted into the teacher's own register | `TeacherAttendanceQueuedScan.isAssignedToSession` defaulted to `true`, so a QR payload the roster search could not match advanced "91 of 119" as if it belonged to the class — and the scanner hands that number to the saved screen with no reload. Membership-linked students were never the issue (the roster query covers `sessionId OR linkedIds.Contains(...)`, so they resolve as `false`). Fixed 2026-09-14: the flag is `bool?`, defaults to null, and only `true` moves the register. **When the two errors are asymmetric — over-counting says "done" while a student is unmarked, under-counting only reads one short — encode "unknown" and take the safe one.** |
| BUG-27 | `AdminSubscriptionService.ActivateCoreAsync` invented a price | Admin activation bypasses payment — the money is arranged outside the app — but the method priced the period through `SubscriptionPricing.MonthlyValueEGP` and stored it as `AmountPaidEGP`. The teacher's own subscription history (`GET /api/subscription/history`) reads that column straight out, so an admin granting a free trial or a goodwill month showed the teacher money they had never paid, in the one screen a teacher would quote back in a dispute. It had been hardcoded `0m` before. Fixed 2026-09-14: `AmountPaidEGP` is a nullable field on `AdminActivateRequest` / `AdminActivateManagerialRequest` (null = "not stated", NOT zero — the `AdminExtendRequest` pattern), omitted stores `0m`, and a stored `0` now renders as "no amount recorded" because `SubscriptionHistoryItemDto.AmountPaidEGP` became `decimal?` and maps `0m → null`. **No schema change** — the entity column stays `decimal NOT NULL`. **The server must not fabricate a number a human never stated; a null is a truthful record and a zero is not.** See §7.11. |
| BUG-28 | `ExamService.CreateExamAsync` silently truncated the exam paper | It did `dto.AttachmentFileIds.Distinct().Take(MaxAttachmentsPerExam)` and created the exam anyway, so a teacher attaching twelve photographed pages got a **201 and a question paper that stops at page ten**, with nothing on screen saying so — while the dedicated endpoint (`AddExamAttachmentsAsync`) answered the identical rule with 422 `ExamTooManyAttachments`. A truncated paper is worse than none: the student cannot tell which it is. Fixed 2026-09-14 — the count is checked in step 1 before the transaction opens and returns the same key and the same 422. **Two paths enforcing one rule must give one answer, and the answer to "too many" is never "here are the first ten".** See §7.9. |
| BUG-29 | Exam-paper file endpoint ignored the exams-module visibility switch | `FileAccessService.IsReleasedExamAttachmentForStudentAsync` checked the student's obligation and the release gate but never `TeacherConfiguration.StudentVisibilityExamDefault`. Release is permanent by design, so a student who had ever been handed a `fileId` kept fetching the paper after the teacher hid exams — hiding the module reached the list and nothing else. Fixed 2026-09-14 by denying first on the flag, **fail-closed on a missing config row**, read off the configuration the method already loads (no extra query); the identical expression was added to the list side (`ExamHomeworkService.LoadReleasedExamAttachmentsAsync`) so the two gates stay in step. **A category policy in the file registry must include the owning module's own visibility switch, not only the resource gate** (§5.5, §7.9). |
| BUG-30 | HEIC exam papers were unviewable by every student | The backend `UploadConstants` allow-list accepts `image/heic` / `image/heif`, and HEIC is the iPhone camera default — but Flutter/Skia cannot decode HEIC, HEIF or TIFF. So an iPhone-photographed exam paper uploaded cleanly, **rendered perfectly in the teacher's own browser preview**, and failed to render for every student in the class. The one surface that showed the failure was the only one nobody looked at. Fixed 2026-09-14 (app): every picked attachment IMAGE is re-encoded to JPEG at pick time regardless of size (quality 95 / 3000px long edge; the pre-existing over-limit path keeps 80 / 1600px), and a decode failure **refuses with a specific message** instead of uploading the original. **Follow-up still open: the server allow-list has not changed**, so any other client can still store a file nothing renders. **An upload allow-list must be the intersection of what the server accepts and what the viewers can decode — the teacher's preview is not the test.** See §7.9. |
| BUG-31 | Books & fees cash moved the wallet but appeared in NO ledger | `EventPaymentTransaction` credited `AssistantWallet.CurrentBalance` on collect, yet every ledger read (`GET /api/v1/payments/collections` on both its teacher-wide and collector-scoped paths, the wallet screen's signed stream, the collectors cards, the live collections count) queried `PaymentTransactions` alone. So a collector's balance could exceed the sum of the rows their own ledger listed, and `HeldSinceAt` — the anchor that makes "in hand now" scope exactly to the held cash — was computed from an incomplete event stream and could land on the wrong event. Latent in practice only because no client ever called the module (prod-verified 2026-09-14: 0 payments). Fixed by `CollectionLedgerQueries` unioning both tables with a `kind` filter defaulting to `fees`, and by folding extras collections AND refunds into the wallet's balance reconstruction. **Never re-separate the wallet from the ledger that explains it**: if a figure moves `CurrentBalance`, it must be a row somewhere. See §7.12. |

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

## 11b. Time, dates & timezones (hardened 2026-09-06)

Authoritative detail: **`TIMEZONE_STANDARD.md`** (backend) and the app's own
`TIMEZONE_STANDARD.md` (Flutter). The short version:

- **Three kinds, three types.** A moment-in-time is a `DateTime` in UTC; a calendar day is a
  `DateOnly` (wire `"2026-09-06"`); a wall-clock time-of-day is a `TimeOnly` (wire
  `"22:53:00"`). Pick the type by what the value MEANS — the wire cannot tell them apart on
  its own, and that ambiguity is what produced the 2-3h bugs.
- **The wire now states its zone.** `UtcDateTimeJsonConverter` /
  `NullableUtcDateTimeJsonConverter` (`Edvanz.Application/Json/`, registered in `Program.cs`
  AND repeated in `TeacherStudentController.StreamJsonOptions`) serialize every `DateTime` as
  `...Z`. `Unspecified` is STAMPED, never shifted — everything persisted is already UTC.
  Reading is left at the framework default, so what callers may send and what gets persisted
  are unchanged. Two fields opt out per-property with
  `LocalWallClockDateTimeJsonConverter` because they are deliberately the teacher's LOCAL
  wall-clock: `PaymentTransaction.LocalCollectedAt` and the student tracking `PaidOnDate`.
- **Never retype a REQUEST DTO field to `DateOnly`** — deployed clients send full ISO strings
  and `DateOnly` cannot read them, which 400s them. Responses only.
- **A business "today" is the TENANT's**, via `ITimeZoneService.GetTeacherLocalDate` — never
  `DateTime.UtcNow.Date`, which is still YESTERDAY between midnight and 2-3 AM Cairo. A repo
  that needs "today" takes it as a REQUIRED parameter from its calling service
  (`ISessionRepo.BuildSessionListQuery`, `IPaymentRepo.GetActiveSessionsCollectionSummaryAsync`)
  rather than reading a clock, so the compiler enforces the rule.
- **Two deliberate exceptions, do not "fix" them**: `AttendanceAutoAbsentService` uses a coarse
  UTC window on purpose (the per-teacher worker re-gates locally); the subscription module
  compares `EndDate.Date` to `UtcNow.Date` throughout and is internally consistent, with its
  dispatcher at 09:00 Cairo where both dates agree — migrate it whole or not at all.
- **Enforcement.** `scripts/check-datetime-usage.sh` runs as a CI gate before the migration
  gates and fails on `DateTime.Now`, `DateTime.Today`, `.ToLocalTime()` and
  `DateTime.UtcNow.Date` outside the allowlist. On the app side
  `test/core/api_date_time_guard_test.dart` fails on a bare `DateTime.parse`/`tryParse` outside
  `lib/core/utils/api_date_time.dart`; models parse through `parseApiUtcDateTime` (instant) /
  `parseApiCalendarDate` (day) / `parseApiLocalDateTime` (already-local) /
  `parseApiDurationMinutes` (a .NET `TimeSpan` such as `"01:01:00"`, which `int.tryParse`
  cannot read — it silently became 0 on the student exam list).
- **Fixed with this change (do not reintroduce):** the student online-exam list re-read the
  already-Cairo `examDate` + `examTime` pair as UTC, so a 22:53 exam showed at 01:53 the NEXT
  day and its entry gate ran 2-3h late while the take screen (real `startDateTime` instant)
  showed the right time; `duration` (a `TimeSpan`) parsed to 0 so every card read "0 min";
  `prorationSetAt` (an audit instant) was read as a calendar day; scheduled messages built
  `UtcNow.Date + 09:00` and so went out at noon Cairo; a session transfer between midnight and
  3 AM dated its carried-forward period into the PREVIOUS billing month.

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
| Hard-deleting `FileObject` rows or blobs inline | Only `file-object-gc` reaps (Detach instead); inline deletes orphan blobs (§5.5) |
| `DateTime.UtcNow.Date` as a business "today" | Still YESTERDAY between midnight and 2-3 AM Cairo; use `ITimeZoneService.GetTeacherLocalDate` (§11b) |
| A calendar day typed `DateTime` on a response DTO | Use `DateOnly` so the wire says it is a day; a `DateTime` now carries a meaningless `Z` (§11b) |
| Retyping a REQUEST DTO field to `DateOnly` | Deployed clients send full ISO strings; `DateOnly` cannot read them and 400s them (§11b) |
| Bare `DateTime.parse`/`tryParse` in a Flutter model | Silently assumes device-local and shifts an instant by 2-3h; use the `api_date_time.dart` helpers (§11b) |
| Re-enabling anonymous blob access / exposing `BlobPath` | Files are JWT-gated via `/api/files/{fileId}`; anonymous blob 401/403/409 is intended (§5.5) |
| Leaving a denormalized `TeacherId` navigation to EF convention on a scope-style child | Convention creates the FK with **CASCADE**, against the NoAction default (§4.2). Declare `HasOne(x => x.Teacher)…OnDelete(NoAction)` explicitly, AFTER the composite FK — `OnlineExamScope` is the working recipe (§7.12) |
| `HasPrincipalKey` + a separate `HasIndex(...).IsUnique()` for a composite-FK target | Produces TWO identical unique indexes (`AK_` and `UX_`). Use `HasAlternateKey(...).HasName("AK_…")` — documented at `EdvanzDbContext.cs:1333`, and re-learned on `PaymentEvents` (§7.12) |
| `Union` where rows must be counted individually | `Union` DEDUPES: two students paying the same amount at the same instant would collapse into one. Use `Concat` for a ledger; `Union` only where a duplicate is genuinely the same thing (`VideoAudienceQueries`) — §7.12 |
| A `Count(predicate)` per population in one projection | Re-inlines the row's correlated members once per reference, and a predicate that folds to a constant becomes literal `COUNT(NULL)`, which SQL Server rejects (BUG-16). Project a typed bucket via `CASE` and use ONE `GroupBy` (§7.12) |
| Reading a cached aggregate column a response depends on | `PaymentEvent.TotalStudents`/`TotalExpectedRevenue`/`TotalCollectedRevenue` were clobbered independently by three write paths. Derive from the rows in one grouped query keyed on the page's ids (§7.12) |
| Adding a scope-style table without wiring session/group hard-delete | Both target FKs are `NoAction` and the CHECK forbids nulling them, so a surviving row fails the delete with a 409. Add the two `Delete…ScopesBy{Session,Group}Async` calls in `SessionService` beside the existing three (§7.12) |

<!-- ci: markdown-only edits do not trigger the deploy workflow (paths-ignore). -->
