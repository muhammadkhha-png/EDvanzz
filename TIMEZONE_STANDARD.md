# Timezone & Date-Time Standard (Backend — Edvanz.API)

One rule set for every point-in-time and calendar value in the .NET solution. Follow it
without exception; it exists because subtle UTC/local mistakes shipped real bugs (online-exam
times shown 2–3h early, "X ago" off by the Egypt offset).

Default timezone for the product is **`Africa/Cairo`** (UTC+2 winter / UTC+3 summer).

---

## 1. Persistence — always UTC, always `DateTime.UtcNow`

- Every stored moment-in-time is UTC. Write `DateTime.UtcNow` (never `DateTime.Now`).
  Examples: `CreateAt`, `CreatedAt`, `UpdatedAt`, `SubmittedAt`, `CollectedAt`,
  `RequestedAt`, `ResolvedAt`, `DepartedAt`, `RefundedAt`, `EditedAt`, `LastCollectionAt`,
  `StartDateTime`/`EndDateTime`, `PublishDate`.
- A **calendar-only** value (a day with no meaningful time-of-day) is a `DateOnly`, or a
  `DateTime` whose date component is the payload and whose time is an ignored midnight.
  Examples: `PeriodStart` (billing month), exam `ExamDate`/`DueDate`, `AssignmentDate`,
  attendance occurrence date, `SessionOccurrence.OccurrenceDate`, subscription `StartDate`/
  `EndDate`, `JoinedAt` (first-attendance/assignment day), `OverdueOn`. Never stamp these
  with `UtcNow`'s time-of-day.
- **`LocalCollectedAt` is the deliberate exception**: it is stored as the teacher's LOCAL
  wall-clock (Kind `Unspecified`) so a receipt shows the time the tutor actually collected
  cash. It must never be treated as UTC. Its sibling `CollectedAt` is the UTC instant.

## 2. Any UTC↔local conversion goes through `ITimeZoneService`

`ITimeZoneService` (impl `Edvanz.Infrastructure/Services/TimeZoneService.cs`) is the single
place that knows the timezone and handles the DST gap:

- `GetTeacherLocalNow(teacherId)` / `GetTeacherLocalDate(teacherId)` — "now"/"today" in the
  teacher's zone. Use for defaulting the selected month, "today's sessions", auto-absent, etc.
- `ConvertUtcToLocal(utc, "Africa/Cairo")` — render a stored UTC instant in local time, or
  bucket it to a local day. **Use this before truncating a UTC instant to a `DateOnly`/
  `TimeOnly`/day.**
- `ConvertLocalToUtc(local, "Africa/Cairo")` — turn a caller-supplied local filter bound into
  UTC before querying UTC columns.

**Never** truncate a UTC instant to a local date/time without converting first:

```csharp
// WRONG — truncates the UTC instant; shows 2–3h early in Cairo
ExamDate = DateOnly.FromDateTime(exam.StartDateTime),
ExamTime = TimeOnly.FromDateTime(exam.StartDateTime),

// CORRECT — convert UTC → Cairo first
var localStart = _timeZoneService.ConvertUtcToLocal(exam.StartDateTime, "Africa/Cairo");
ExamDate = DateOnly.FromDateTime(localStart),
ExamTime = TimeOnly.FromDateTime(localStart),
```

Server-generated static artifacts (PDF/Excel exports, "generated at" stamps) have no client to
localize them, so they must convert through `ITimeZoneService` at build time (see
`AuditTrialService`), or label the value `UTC` explicitly.

## 3. The wire format is intentionally NOT changed

The live production mobile build parses no-`Z` `DateTime` strings as **local**. We do **not**
add a global JSON converter that stamps `Z` on every `DateTime` — that would move what the
deployed app renders and would also mis-handle the calendar-date fields. The wire contract
stays as-is; the **new** mobile build normalizes on the client (see the app's
`TIMEZONE_STANDARD.md` + `parseApiUtcDateTime`). If a specific instant ever needs an explicit
`Z`, prefer per-field `DateTime.SpecifyKind(x, DateTimeKind.Utc)` at the point it is built and
document the deployed-app impact first.

## 4. Checklist when adding a field

1. Is it a moment-in-time or a calendar day? Moment → UTC (`UtcNow`); day → `DateOnly`/date-only.
2. Displaying or day-bucketing a UTC instant? Convert via `ITimeZoneService` first.
3. Never `DateTime.Now` / `DateTime.Today` / `.ToLocalTime()` in business code.
4. Deriving "today"/"this month"? Use `GetTeacherLocalDate` / `GetTeacherLocalNow`.
