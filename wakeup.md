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
- NOTE ON DEPLOYS: on B1 the container swap causes intermittent 503s for ~1–2 min and can serve the
  OLD build briefly; always wait for a stable streak (or `az webapp restart`) before trusting a test.
- (continuing: sessions/attendance/payments write scenarios, perf pass, final report…)
