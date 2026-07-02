# Swagger Documentation — Phase 3: Session Module

> Handoff spec for Claude Code. Execute **directly in the repo**. Read the referenced
> files before editing. Modify **only** the Session module (+ `Program.cs` registration).

## Context (already exists — do not redo)

Phases 1 (Auth) and 2 (TeacherStudent) are complete. The Swagger example infrastructure
is a **provider registry**:

- `Edvanz.API/Filters/IEndpointExampleProvider.cs` — `IEndpointExampleProvider`, abstract base
  `EndpointExampleProvider` (with `SuccessEnvelope` / `FailureEnvelope`), and the
  `EndpointExampleSet` record supporting single (`RequestBody`, `Responses`) **and** named
  dropdown (`RequestBodyExamples`, `ResponseExamples`) examples.
- `Edvanz.API/Filters/SwaggerExamplesFilter.cs` — thin `IOperationFilter`. **Never edit to add a module.**
- `Edvanz.API/Filters/AuthExampleProvider.cs`, `TeacherStudentExampleProvider.cs`,
  `SubscriptionExampleProvider.cs` — reference implementations. Mirror their style.
- `Edvanz.API/Program.cs` — providers registered as singletons **before** `AddSwaggerGen`.

Response envelope (`ApiBaseController.ToResponse`): `{ "success": bool, "message": string, "data": object|null }`.

## Objective

Fully document **only** the Session module in Swagger:

1. Convert the controller's `//` "WHAT IT DOES / TABLES / SAMPLE" blocks to `///` XML docs
   (`<summary>`, `<remarks>`, `<param>`, `<response>`).
2. Complete `[ProducesResponseType]` for every status each action can return.
3. Add `SessionExampleProvider : EndpointExampleProvider` with realistic examples from real
   seeded data.
4. Register the provider in `Program.cs`.
5. Touch no other module.

## Files to read first (authoritative)

- `Edvanz.API/Controllers/SessionController.cs` — enumerate **every** action: verb, route
  template, request DTO, route/query params. Source of truth.
- `Edvanz.Application/Dtos/Session/SessionDtos.cs` — `CreateSessionDto`, `UpdateSessionDto`,
  `CreateSessionGroupDto`, `RenameSessionGroupDto`, `CreateSessionLinkDto`,
  `AssignStudentsToSessionDto`, `SessionListRequest`, and output `SessionDto`. **Use exact
  property names/casing — do not guess.**
- `Edvanz.Application/Services/SessionService.cs` — for each failure branch, note the exact
  `_localizer["Key"]` used (needed for Rule C below).
- `Edvanz.Domain/Messages.en.resx` — the English values behind those keys.
- `Edvanz.API/Filters/TeacherStudentExampleProvider.cs` — copy structure exactly.

## Rules carried from Phase 2 feedback (MANDATORY — apply to every branch)

### Rule A — route keys are normalized, `:long` stripped, lowercased
Session routes carry `{teacherId:long}` / `{sessionId:long}` constraints. Swagger's
`RelativePath` drops the `:long`; the filter lowercases and trims slashes. Provider branch
keys MUST be the normalized form, e.g.:
- `POST api/session`
- `GET  api/session/{teacherid}/sessions`
- `GET  api/session/{teacherid}/sessions/{sessionid}`
- `PUT  api/session/{teacherid}/sessions/{sessionid}`
- `POST api/session/{teacherid}/sessions/{sessionid}/duplicate`

Confirm each route against the controller; do not assume the segment layout.

### Rule B — list/query endpoints have NO request body
`GetSessionList` takes `[FromQuery] SessionListRequest`, so `operation.RequestBody` is null —
a request-body example (`RequestBody` / `RequestBodyExamples`) will NOT render. For these:
- Document each query field via `<param>` on the action and rely on the `[Description]`
  attributes already on `SessionListRequest`.
- Put the "no filters vs filtered" contrast on the **response** using named
  `ResponseExamples["200"]` (e.g. `"Unfiltered — full page"` vs
  `"Filtered — search='Monday', activeOnly=true"`), which is a real dropdown that renders.

### Rule C — failure example messages must be the real localized strings
Do not invent failure text. For every non-2xx example, find the exact `_localizer["Key"]` the
service returns at that branch in `SessionService.cs`, then copy the English value verbatim
from `Messages.en.resx`. Example: the 409 on `UpdateSession` is the **business** conflict
"occurrence type locked when the session has assignments/links" (REQ-SES-009) — it is NOT
optimistic concurrency (no `RowVersion` exists). Use that branch's actual message key.

## Auth & tenancy note (state in XML docs, do not change behavior)

Session is an **older module**: `teacherId` comes from the **route** (`[FromRoute] long teacherId`),
unlike the JWT-only resolution in TeacherStudent/Module-6. Document it exactly as implemented,
and add a one-line `<remarks>` note that this differs from the JWT-resolved modules. Flag the
inconsistency in the phase report — do not "fix" it here.

## Real seeded data for examples (from `DbInitializer`)

Default password: `Edvanz@2026`.
- Teachers: `teacher1` (Ahmed Mostafa, `T0000001`), `teacher2` (Mariam Hassan, `T0000002`).
- Seeded sessions: **"Session A1"** (under teacher1) and **"Session B1"** (under teacher2) —
  `OccurrenceType = Weekly`, `PaymentType = PerSession`, `SessionAmount = 100` EGP, date range
  roughly ±30 days from seed time.
- Students: `student1` (Youssef Tarek, `STU000001`) — usable in the "assign students" example.

**ID rule:** numeric `id` / `teacherId` / `sessionId` are identity-generated and NOT stable —
illustrative only. Prefer session names ("Session A1") and teacher codes; label numeric ids
with an inline comment, exactly as the other providers do. Serialize enums by their string
names (`"Weekly"`, `"PerSession"`, `"Monthly"`) to match the project's converter.

## Open issue carried forward (verify, don't silently fix)

`Result.Failure(...)` default HTTP status — confirm from `Result.cs` before finalizing
`[ProducesResponseType]` and response-example keys. If Phase 2 already confirmed it, reuse that
value and note it. (Session actions also explicitly return 201/400/404/409 via attributes —
match those.)

## Steps (in order)

1. Read `SessionController.cs`; list every endpoint (verb, normalized route per Rule A, DTO, params).
2. Read `SessionDtos.cs`; record exact property names/casing.
3. For each failure branch, read `SessionService.cs` + `Messages.en.resx` for the real message (Rule C).
4. Edit the controller: `//` → `///` for every action (`<summary>`, `<remarks>` incl. the
   route-teacherId tenancy note, `<param>` per param, `<response>` per status); complete
   `[ProducesResponseType]`. Docs/attributes only — change no behavior.
5. Create `Edvanz.API/Filters/SessionExampleProvider.cs`:
   - `public sealed class SessionExampleProvider : EndpointExampleProvider`
   - `public override EndpointExampleSet? GetExamples(string method, string route)`
   - One branch per endpoint, keyed by the normalized route (Rule A).
   - Body endpoints (create/update/group/link/assign): `RequestBody` example from seeded data.
   - List endpoint: response-only, named `ResponseExamples["200"]` per Rule B; paginated `data`
     shape (`data`/`page`/`pageSize`/`totalCount`) matching `SubscriptionExampleProvider`.
   - Failure examples use real localized strings (Rule C).
   - Fresh `JsonObject`/`JsonArray` per example (a `JsonNode` has one parent; reuse throws).
   - End with `return null;`.
6. Register in `Program.cs`, before `AddSwaggerGen`:
   ```csharp
   builder.Services.AddSingleton<IEndpointExampleProvider, SessionExampleProvider>();
   ```
7. Build; fix issues.

## Acceptance checklist (all must pass)

- [ ] Solution builds clean (no new errors/warnings).
- [ ] Every `SessionController` action: `///` summary + remarks, `<param>` per parameter,
      `<response>` per status; `[ProducesResponseType]` matches reality.
- [ ] `SessionExampleProvider` has one branch per endpoint; every route key is the normalized
      `:long`-stripped lowercase form (Rule A).
- [ ] List endpoint uses response-side named examples, NOT a request-body example (Rule B).
- [ ] Every failure example message matches the real `Messages.en.resx` value (Rule C).
- [ ] Provider registered; DI resolves at startup.
- [ ] `/swagger`: every Session operation shows summary, param notes, and example bodies/responses.
- [ ] Regression: Auth, TeacherStudent, Subscription examples unchanged.
- [ ] Only Session-module files + `Program.cs` modified.

## Report back

Summary of work; modified/created files; confirmed `Result.Failure` status code; the
route-based-`teacherId` inconsistency flag; any other issues; suggestions before Phase 4 (Attendance).

---

## Run with Claude Code

1. Save this file to the repo, e.g. `docs/swagger-phase-3-session.md`, and commit it.
2. Open a terminal at the repo root (folder with `CLAUDE.md`) — VS integrated terminal is fine.
3. `claude`, then:
   > Read docs/swagger-phase-3-session.md and CLAUDE.md, then execute Phase 3 exactly as
   > specified. Apply Rules A, B, and C to every branch. Read the referenced files before
   > editing. Stop at the acceptance checklist and report back.
4. Review the proposed diff before accepting; build; open `/swagger`; walk the checklist.
