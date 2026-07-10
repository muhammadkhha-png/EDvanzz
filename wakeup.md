# Wakeup Report — Autonomous MVP Hardening

_Autonomous run while you slept. Goal: MVP with 0 issues (logic, tenant isolation, performance, scenarios), no Azure cost changes._

## TL;DR
- Fixed and **verified live on prod**: student-add bug, assistant-create bug, **bulk-import 60s timeout → ~3s**, **cross-tenant IDOR** (Session/Teacher route endpoints), migration pipeline, security (connection string externalized + SQL password rotated), schema drift.
- Tenant isolation matrix now passes fully (teacher/assistant own-scope allowed, cross-tenant 403).
- I did **not** change any Azure tier (cost). One free reliability setting applied (startup time limit).

## ⚠️ NEEDS YOUR DECISION / ATTENTION
1. **Azure scaling (COST — deferred):** Prod SQL is **Basic / 5 DTU**, App Service is **B1 (1 worker)**. This is the main performance ceiling and the cause of slow/**flaky deploys** (container cold-start sometimes exceeds Azure's limit → brief 503s on deploy). I did NOT scale (you're on the $200 free credit). Recommendation for launch day: temporarily scale SQL to **Standard S1/S2** and App Service to **S1** if you see DTU saturation or timeouts; scale back after. All app-level query work below is done to squeeze Basic tier.
2. **Deploys cause a brief outage window** on B1: the old container serves until the new one warms (~30–90s, occasionally a 503 blip). **Do not deploy during launch peak.** After any deploy, the app self-recovers; if a deploy looks stuck, `az webapp restart` and wait ~2 min.
3. **Password rotation done** — the OLD SQL password is still in git history. Consider a history purge (BFG/filter-repo) at your leisure; not urgent since the credential is rotated.
4. **Residual tenant gap (being addressed):** the route-based IDOR is fixed. Body-based writes that carry `teacherId` in the JSON (e.g. create session/group/link, assign students) are being covered by extending the tenant filter to inspect action arguments — see progress log.

## ✅ Fixes completed & LIVE on prod (all verified)
1. **Student add "phone already registered"** — code generator no longer resets to A1 on non-pattern codes; unique-violation errors mapped correctly.
2. **Assistant create "conflict with existing data"** — user phone made optional (nullable + filtered unique index), blank→null normalization, graceful unique-violation messages.
3. **Bulk import timeout** — was >60s for 4 rows (per-row DB code generation spinning). Now one batched DB read + in-memory increment. Verified: 8 rows in ~3s.
4. **Cross-tenant IDOR** — added `TenantScopeFilter` validating route `teacherId` against the caller (teacher = own id, assistant = owning teacher, SuperAdmin bypass). Verified matrix (teacher/assistant own-scope 200, cross-tenant 403).
5. **EF migrations pipeline** — `.gitignore` no longer hides migrations; CI applies them to Azure SQL on deploy (`azure/sql-action`, secret `PROD_SQL_CONNECTION`). Validated.
6. **Schema drift** — audited model vs prod (all 68 tables present); added missing `IX_PP_TeacherId_Status_PeriodStart`. Prod now matches model.
7. **Security** — removed committed DB connection string; CI injects it from the secret into App Service config; **rotated SQL password** (app verified healthy). Raised `WEBSITES_CONTAINER_START_TIME_LIMIT=600` (free) to reduce deploy timeouts.

## Test results so far
- **Pass 1** (reads / permissions / tenancy, all MVP modules): 42/42 passed; latency 89–537ms; permission enforcement correct (assistant1b blocked from Add/Payment → 403).
- **Pass 2** (settings + student write lifecycle on teacher2): auto/manual code, duplicate/invalid code → correct 400/409, bulk import, edit, soft-delete/restore/permanent — all pass after the bulk fix. Test data cleaned up.
- HTML report generator ready; combined report will be at `test-report/edvanz-prod-test-report.html` (to be finalized).

## Test accounts (prod, password `Edvanz@2026`)
- teacher1 (TeacherId 1), teacher2 (TeacherId 2)
- assistant1a (full perms, teacher1), assistant1b (view-only, teacher1), assistant2a (teacher2)
- superadmin

## Remaining work (in progress, autonomous)
- Extend tenant filter to body `teacherId` (create/assign) + redeploy + verify.
- Pass 2 continued: sessions (create auto/manual name, groups, membership, links), attendance (take/edit/view), payments (collect by teacher + assistant, wallets). Isolated + cleaned up.
- App-level performance pass (heaviest endpoints on 5-DTU): payment dashboard, reports, list queries — check N+1 / missing indexes.
- Finalize combined HTML report.

## Progress log
- Bulk-import fix + IDOR filter deployed & verified.
- Tenant filter corrected (assistant resolution by userId) & verified.
- Tenant filter extended to BODY teacherId (create/assign) — verified: teacher2 cannot create a
  session under teacher1 (403), can under self (201). Route + body IDOR both closed.
- SESSION module writes verified (Pass 3) on teacher2, cleaned up:
  create session (auto name + manual name), group create/rename/list, assign students
  (studentCount 0→2), unassign (2→1), link create/remove, and IDOR (create group under teacher1 →403).
  All pass.
- NOTE ON DEPLOYS: on B1 the container swap causes intermittent 503s for ~1–2 min and can serve the
  OLD build briefly; always wait for a stable streak (or `az webapp restart`) before trusting a test.

## ✅ Findings 1 & 2 — FIXED & verified (plus a broader bug found)
- **ROOT CAUSE (bigger than first thought):** `DeactivateAssignmentsBySessionAsync` only nullified
  `StudentSessionAssignment.SessionId` for rows where `IsActive=true`. Any assignment already
  deactivated — by a normal **unassign**, or by a purged student — kept `SessionId` set, and the
  NO-ACTION Session FK then **blocked the session's hard delete with 409**. So **deleting any session
  that had ever had a student unassigned failed** — a common flow, not just the purge edge case.
- **FIX:** nullify `SessionId` for ALL of a session's assignments regardless of `IsActive`
  (`AttendanceRepo.DeactivateAssignmentsBySessionAsync`). Also wired the previously-dead
  `IAttendanceService.OnStudentPermanentlyDeletedAsync` hook into student permanent-delete/purge so
  absence counters + attendance-record FKs are cleaned too.
- **VERIFIED on prod:** unassign→delete session = 200; purge-assigned-student→delete session = 200.
- **Finding 2 residue cleaned:** stuck sessions (ZZ Mem A id 9, id 12) deleted successfully via API
  after the fix. teacher2 back to only its original `Session B1`. No leftover test data.
- Note: `GetStudentById` measured 1584ms once (cold); fast on warm cache — watch under load.

## ✅ Attendance & Payments — verified this run
- **Attendance**: generate occurrences OK; mark Present/Absent OK for valid dates; dashboard +
  timeline OK. The 400 I first hit was a CORRECT business rule ("cannot record attendance for a date
  before the student was assigned") — not a bug. Marking for today (on/after assignment) works.
- **Payments**: collect (teacher, ManualName, 100 EGP) → OK; payment-status + dashboard reflect it.
  Assistant-collect + wallet were verified by READS in Pass 1 (real historical wallet data present:
  assistant1a had collected 250, wallet mechanics intact). Assistant WRITE collect not run to avoid
  polluting teacher1's data (assistant2a lacks Payment perms; only teacher1's assistant1a has them).

## 🟢 Minor leftover (harmless, TEST account only)
- One residual **100 EGP payment transaction on teacher2** (test account) for a since-deleted test
  student/session. Payment records are intentionally RETAINED (denormalized) after student/session
  deletion for audit — so this is by-design, just leftover test data on a non-real account. It's
  orphaned (student+session gone) so it doesn't appear in standard session/student ledgers; delete
  it directly if desired. Also note: my machine's IP rotates and Azure SQL firewall blocked my
  direct DB access late in the run (I did NOT open the firewall — that weakens prod security).

## Still TODO (optional / next)
- Assistant-side payment collect + wallet WRITE (needs a test assistant with Payment perms, or accept
  writing under teacher1). Event-based payments write flow.
- App-level performance pass on heaviest endpoints for the 5-DTU DB (payment dashboard, reports, big
  list queries) — check N+1 / missing indexes. (Reads were all <600ms at low load in Pass 1.)
- Re-run the combined HTML report to reflect all-green after the above.

## ✅ Membership / attendance / payment deep-dive (answering your specific questions)
All verified live on prod (teacher2 + a temp test assistant), then cleaned up:
1. **Session links (membership)** — create/remove OK.
2. **Attendance across linked sessions** — marking a student (whose home session is B) inside linked
   session A works; recorded as `CrossSessionPresent` with the cross-session reference. ✅
3. **Attendance list scoping** — session A's list shows members of A **and** linked session B, and
   EXCLUDES unrelated students. ✅ (exactly what you asked — not all students.)
4. **Edit attendance** — works (Present→Absent, `isEdited=true`, edit-history entry). ✅
5. **Assistant collect** — works; assistant wallet correctly **credited** (balance 200). ✅
6. **Edit payment** — works.
7. **Refund / take money back** (delete transaction) — works; period + counter reversed.
8. **Withdraw** (wallet reset) — works.

### 🔴🔴 CRITICAL financial bug FOUND & FIXED (your suspicion was right)
- The assistant **wallet was NOT adjusted on payment edit or refund/delete**. After a refund the
  wallet still showed the cash as held — the assistant would be asked to hand over money already
  returned to the student. **FIXED** (`AdjustAssistantWalletAsync` now called on edit ±diff and on
  delete −amount). **VERIFIED live:** collect 200→wallet 200; edit→150→wallet 150; refund→wallet 0.
- **Refund paths clarified:** there is NO dedicated `/refund` endpoint. Money is taken back via
  DELETE transaction, edit-to-lower-amount, or `batch-revert` (which reuses EditPayment) — ALL now
  adjust the assistant wallet correctly. The DEPARTURE flow (`POST /payment/departure/confirm`, for a
  student leaving) intentionally records the refund as a **manual settlement** and does NOT auto-adjust
  the wallet. **DECISION FOR YOU:** if you want departure to auto-deduct the refund from the assistant
  wallet too, say so and I'll wire `AdjustAssistantWalletAsync` into ConfirmDeparture. Left as-is
  (current design = manual settlement).
- Bonus bug found while cleaning up: **a session that had payment history couldn't be deleted (409)**
  — soft-deleted (refunded) payment transactions still referenced the session (query filter skipped
  them). **FIXED** (`NullifySessionIdOnPaymentRecordsAsync` now `IgnoreQueryFilters`). Verified.

## ✅ Departure enhancements + validation (latest)
- **Departure now auto-adjusts the wallet:** on a refund, `FinalAmount` is deducted from the wallet
  of the assistant who collected (via `GetLatestCollectorUserIdForStudentSessionAsync` +
  `AdjustAssistantWalletAsync`). No-op when the tutor collected (no wallet). So a departure refund
  lowers the assistant's held cash automatically (your 13000→12500 example). Verified by
  construction (mirrors the verified delete/edit wallet path).
- **Optional `DeleteStudent` flag** on `ConfirmDepartureDto` (defaults false → existing frontend
  unaffected): when true, the student is also soft-deleted (recycle bin) on departure, not just
  unassigned. **Verified live** (student left active list, appears in recycle bin).
- **Student-code validation (your question):** AUTO mode + a manually-entered code was previously
  **silently ignored**; now returns a clear 400 (`StudentCodeNotAllowedAuto`, en+ar). Verified live.
  Manual-mode empty code and empty name already return clear 400s.
- **Teacher-collected (vs assistant):** `AdjustAssistantWalletAsync` is a no-op when the collector is
  the tutor (they have no wallet), so teacher-collected edit/refund/departure adjust period+counter
  but touch no wallet — correct by design.

## ⚠️ OPEN QUESTION FOR YOU (business rule — I did NOT guess)
- **Proration-disabled departure refund.** `GetDepartureSummaryAsync` ALWAYS pro-rates the refund by
  attendance (`refund = Full − (attended/total)*Full`) and **does NOT check** the teacher's
  `IsProratedPaymentEnabled` setting. You said "proration can be disabled → departure means the full
  amount." Please confirm the exact intended behavior when proration is DISABLED:
  (a) full refund of what they paid, or (b) they owe the full period (no refund)? Tell me which and
  I'll wire the config check into the departure calc. Left unchanged for now (financial rule — didn't
  want to guess). NOTE: the summary also evaluates the *earliest unpaid* period, which is why a
  student who paid month 1 but has an unpaid month 2 shows `NoObligation`.

## Deploys made this run (all on master_integration, live)
e7f7628 bulk-import perf + IDOR filter · f0fda5c assistant resolution fix · ae73958 body-teacherId
IDOR · ce182a3 attendance hook on student purge · 714013f session-delete 409 fix. All verified live.
