# Assistant Payment Dashboard — interim scoping + handoff (TO BE BUILT PROPERLY)

**Status:** interim first-cut shipped in the backend. A dedicated, correctly-designed
assistant payment dashboard is **still to be built end-to-end by BOTH frontend and backend.**
Search the code for `TODO(assistant-dashboard)` to find every touch point.

**Date:** 2026-07-25

---

## The problem this addresses

An assistant (e.g. `nour` under teacher `mohamedatef`) opened the Payment screen and got
**"You are not authorized to perform this action"** even though she held all Payment
permissions. Root cause: the Payment landing screen's load fan-out calls tutor-only
endpoints (`roleOnly: ["Teacher","SuperAdmin"]`) — Dashboard, Wallets, Sessions
collection-summary — which an assistant can never pass regardless of permissions. The whole
screen dies on the first 403.

We do **not** want to show an assistant the full teacher figures. An assistant should see a
dashboard scoped to **their own data only** (what *they* collected, *their* wallet).

## What was implemented now (interim, backend only)

For an **assistant caller**, these read endpoints return **only that assistant's own data**;
for a Teacher/SuperAdmin they are unchanged (full view). Gate moved from `roleOnly` →
`[ModulePermission("Payment","ViewCollectorSummary")]`.

| Endpoint | Assistant sees |
|---|---|
| `GET /api/payment/dashboard` | `CollectedRevenue` = **their own** collected; `PerCollectorBreakdown` = **only themselves**. Teacher-wide `ExpectedRevenue` / `RemainingRevenue` / `PerSessionBreakdown` come back **`null`** (see null-vs-zero note). |
| `GET /api/payment/wallets` | **only their own** wallet; combined total = their own balance |
| `GET /api/payment/wallets/{assistantId}` | **forced to their own** wallet (route id ignored for assistants — cannot read a peer's) |
| `GET /api/payment/collectors` | **only their own** collector summary |
| `GET /api/v1/assistants/{assistantId}/wallet` | **forced to their own** wallet (route id ignored for assistants) |

**Still tutor-only (unchanged):** all money-modifying actions — collect edit/delete, batch
edit/revert, set custom amount, wallet **reset** and **withdraw**, departure, transfer — and
`GET /api/payment/sessions/collection-summary` (a teacher-wide business view, not "own data").

### null-vs-zero rule (requested)
Teacher-wide figures on the dashboard are returned as **`null`, never `0`**, for an
assistant, so the frontend can distinguish "not available to you" from a genuine zero and not
render a misleading `0`. `PaymentDashboardDto.ExpectedRevenue` / `RemainingRevenue` are now
`decimal?` and `PerSessionBreakdown` is nullable.

## Why this is only interim — what frontend + backend must still build

This first-cut reuses existing repo queries and bolts scoping onto teacher-shaped endpoints.
A proper assistant dashboard needs a deliberate design:

- **Backend**
  - A dedicated assistant-dashboard endpoint (e.g. `GET /api/v1/assistants/me/dashboard`) with
    a DTO shaped for the assistant (own collected today/this month, own wallet balance, own
    recent collections, own targets) instead of nulled-out teacher fields.
  - Decide the real semantics of an assistant "expected/target" figure (currently `null`).
  - `GetCollectorSummaryAsync` cross-assistant note: the interim path filters in memory; a
    dedicated repo query keyed by collector user id would be cleaner.
  - Revisit whether `ViewCollectorSummary` is the right permission, or a new
    `ViewOwnDashboard` permission should gate the assistant view.
- **Frontend**
  - Build a distinct assistant Payment screen that calls the assistant-scoped endpoints and
    renders own-data cards — do **not** call the tutor-only cards (dashboard teacher totals,
    all-wallets, sessions collection-summary) on the assistant screen.
  - Treat `null` teacher-wide fields as "hidden", not `0`.

## Code touch points (all tagged `TODO(assistant-dashboard)`)

- `Edvanz.API/Controllers/ModuleSixApiBaseController.cs` — `IsAssistantCaller()` / `AssistantScopeUserId()`
- `Edvanz.API/Controllers/PaymentController.cs` — dashboard / wallets / wallets/{id} / collectors
- `Edvanz.API/Controllers/PaymentScreensController.cs` — `/api/v1/assistants/{id}/wallet`
- `Edvanz.Application/Services/PaymentService.cs` — `GetDashboardAsync`, `GetAllWalletsAsync`, `GetWalletDetailAsync`, `GetCollectorSummaryAsync`
- `Edvanz.Application/Services/PaymentScreenService.cs` — `GetAssistantWalletScreenAsync`
- `Edvanz.Application/Dtos/Payment/PaymentDtos.cs` — `PaymentDashboardDto` nullable fields
