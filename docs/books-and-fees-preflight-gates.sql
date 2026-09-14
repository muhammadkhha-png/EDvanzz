/* ============================================================================
   Books & fees (مذكرات ومصاريف) — PRE-FLIGHT GATES
   ALL READ-ONLY. Nothing here writes, locks or changes anything.

   Run against:  sql-edvanz-prod-weu.database.windows.net  /  Edvanz
   Note the physical table names: the Module entity maps to [Models] and
   TutorModule maps to [TutorModuleAccess] — not the names you'd guess.
   ============================================================================ */


/* ── GATE A — how much data already exists ─────────────────────────────────────
   Decides two things:
     1. Whether migration 20260914170310_BooksAndFeesConcurrencyTokens can ship
        with the rest. Adding a `rowversion` column is a SIZE-OF-DATA operation
        (SQL Server stamps every row), so it is already isolated in its own
        migration: instant at 0 rows, a lock risk at scale.
     2. Whether any collector card figures will visibly jump when books & fees
        cash starts appearing in them (it has always moved the wallet balance).
   Expected, since no client has ever called this module: 0 / 0 / 0.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    (SELECT COUNT(*) FROM [PaymentEvents])            AS items,
    (SELECT COUNT(*) FROM [EventStudentObligations])  AS obligations,
    (SELECT COUNT(*) FROM [EventPaymentTransactions]) AS payments;

-- If payments > 0, these are the teachers whose collector cards will change:
SELECT TeacherId, COUNT(*) AS payments, SUM(AmountPaid) AS total
FROM [EventPaymentTransactions]
GROUP BY TeacherId
ORDER BY total DESC;


/* ── GATE B1 — do the authorization rows exist? ────────────────────────────────
   These are LIVE authorization keys: PermissionHandler matches
   snapshot.Modules on the module NAME and snapshot.Permissions on
   "{Module}.{Permission}". DbInitializer only seeds them when the Permissions
   table is empty, so on a database that was already seeded they may be missing —
   and a missing row means no assistant can ever be granted that permission.

   EXPECTED: 1 module row + exactly 6 permissions —
   View, Create, Edit, Delete, CollectPayment, GenerateReports.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT m.Id AS ModuleId, m.Name AS ModuleName,
       p.Id AS PermissionId, p.Name AS PermissionName, p.IsRestricted
FROM [Models] m
LEFT JOIN [Permissions] p ON p.ModuleId = m.Id
WHERE m.Name = N'Event-Based Payment'
ORDER BY p.Name;


/* ── GATE B2 — which teachers would hit a bare 403? ────────────────────────────
   The "grant every new teacher all modules" loop only landed 2026-07-10
   (TeacherService.cs:245, commit 2720c5c). Teachers who registered before that
   may have no grant for this module, and PermissionHandler fails closed on the
   module gate — so the whole feature 403s for them with no explanation.

   THIS IS THE ONE GATE THAT BLOCKS SHIPPING THE UI (Phase 2).
   ──────────────────────────────────────────────────────────────────────────── */
SELECT COUNT(*) AS teachers_missing_grant
FROM [Teachers] t
WHERE NOT EXISTS (
    SELECT 1
    FROM [TutorModuleAccess] tm
    JOIN [Models] m ON m.Id = tm.ModuleId
    WHERE tm.TutorId = t.Id
      AND m.Name = N'Event-Based Payment');

-- The list, so the backfill can be targeted:
SELECT t.Id AS TeacherId, t.TeacherCode, t.UserId
FROM [Teachers] t
WHERE NOT EXISTS (
    SELECT 1
    FROM [TutorModuleAccess] tm
    JOIN [Models] m ON m.Id = tm.ModuleId
    WHERE tm.TutorId = t.Id
      AND m.Name = N'Event-Based Payment')
ORDER BY t.Id;
-- Backfill (do NOT run as SQL — use the audited admin endpoint, one call per teacher):
--   POST /api/admin/tutor-modules/grant   { teacherId, moduleId }


/* ── GATE B3 — the free-tier quota's current value ─────────────────────────────
   Books & fees is subscriber-only, so migration BooksAndFeesFoundation sets this
   to 0 (idempotently, and it leaves an admin-edited Description alone).
   Reading it first just confirms what the migration will change.
   EXPECTED NOW: 1. AFTER the migration: 0.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT ModuleKey, FreeTierLimit, Description, UpdatedAt
FROM [ModuleQuotas]
WHERE ModuleKey = N'Events';


/* ── SAFETY CHECK — neither new migration is applied yet ───────────────────────
   EXPECTED: 0 rows. If either appears, the schema is ahead of the deployed code.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT MigrationId, ProductVersion
FROM [__EFMigrationsHistory]
WHERE MigrationId LIKE N'%BooksAndFees%'
ORDER BY MigrationId;

-- And the last few applied, for context:
SELECT TOP 5 MigrationId FROM [__EFMigrationsHistory] ORDER BY MigrationId DESC;
