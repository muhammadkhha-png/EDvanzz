# Video Content Management (Module 14) — API ↔ UI Audit & Gap Report

> **Purpose:** Evidence-based API-to-UI audit of the Teacher Videos module, structured as an
> implementation-ready backlog for Claude Code CLI. Every claim traces to a Figma node id or a
> repository `file → handler/method` reference.
>
> **Audience:** PM · Tech Lead · Backend implementer (Claude Code CLI).

---

## 0. Audit Metadata

| Field | Value |
|---|---|
| Module | Video Content Management (Module 14 / VCM) |
| Figma canonical (Source of Truth #1) | Section `762:51776` — "videos" (Teacher flow) |
| Requirements (Source of Truth #2) | `Edvanz_Requirements.pdf` → VCM-FR-01..04 |
| Backend (measured against #1 and #2) | repo `BelalMuhamed/EDvanzz`, branch `master_integration` |
| Student-facing screens | **UNKNOWN / Not Designed** in `762:51776` — excluded from comparison |
| Endpoints audited | 10 (+1 `parent` endpoint confirmed **not shipped**) |
| Canonical screens inventoried | 15 |
| Keys / params checked | ~55 |

**Source-of-Truth order (governs how gaps are recorded):**
`Figma 762:51776` → `PRD (VCM §Module 14)` → `Backend`. The backend is compared against the two
references, never the reverse.

**Provenance legend (why an item counts as a gap):**

| Tag | Meaning |
|---|---|
| `[PRD]` | Stated in VCM-FR and shown in UI |
| `[ProdDecision]` | Approved with PM even if not in PRD |
| `[Analytics]` | PRD requires the data + UI shows it + backend returns less/different |
| `[OPEN-bleed]` | Figma-only, not in PRD, shows shared-composer / template-bleed markers — **not a gap** |
| `[OPEN-struct]` | Structural item requiring a product decision before it can be classified |

**Severity scale:** `Critical` (blocks core module behaviour / data-model change) · `High` · `Medium` · `Low`.

---

## 1. Executive Summary

### 1.1 Gap Register (headline)

| ID | Type | Severity | Impl. Order | Provenance | One-line |
|---|---|---|---|---|---|
| **G-UNIT** | Structural / Domain-model | **Critical** | 4 (epic) | ProdDecision | No `VideoUnit` entity; UI models a Unit → Video hierarchy the backend lacks entirely |
| **G-EDIT** | Functional (endpoint) | **High** | 5 (after G-UNIT) | ProdDecision | No `PUT /videos/{id}`; video is immutable after create. Supporting `GET /videos/{id}` sits under this |
| **G-ANL** | Analytics (grouped) | **High** | 1–3 | Analytics / PRD | Analytics endpoints return less than the UI shows — 4 sub-gaps below |
| ↳ G-ANL-1 | Missing response key | High | 2 | Analytics / PRD-03.4 | `completedCount` aggregate absent (S7 "(15)Completed") |
| ↳ G-ANL-2 | Missing response key | Medium | 2 | Analytics | `unseenCount` aggregate not returned (derivable) (S7 "(16)Unseen") |
| ↳ G-ANL-3 | Mismatch | High | 1 | Analytics | List "seen" should be **distinct students**; backend returns `totalOpens` (raw, counts re-opens) |
| ↳ G-ANL-4 | Missing request param | High | 3 | Analytics / PRD-03.3 | `statusFilter` (seen/unseen/completed) absent on `GET /analytics` (S10/S11/S12) |

### 1.2 Counts

- Confirmed **missing endpoints** (functional, independent): **1** (`PUT /videos/{id}`; supporting `GET /videos/{id}` nested under it).
- Confirmed **structural gap**: **1** (G-UNIT).
- Confirmed **missing response keys**: **2** (`completedCount`, `unseenCount`).
- Confirmed **missing request params**: **1** (`statusFilter`).
- Confirmed **mismatches**: **2** (seen raw-vs-distinct; unseen derived-not-returned).
- **Consolidation opportunities**: **3** (C-1 overview BFF; C-3 create+scopes atomic; C-2 documented "keep separate").
- **Open questions** (excluded from counts): 6 template-bleed items.

### 1.3 Recommended Implementation Order (two tracks)

**Track A — Analytics quick wins (independent, additive, non-breaking):**
1. **G-ANL-3** — fix "seen" semantics to distinct-student count (data-correctness).
2. **G-ANL-1 / G-ANL-2** — add `completedCount` + `unseenCount` aggregates to `GET /analytics`.
3. **G-ANL-4** — add `statusFilter` query param to `GET /analytics`.

**Track B — Structural epic (sequence deliberately):**
4. **G-UNIT** — introduce `VideoUnit` domain + data model (foundational; see §3).
5. **G-EDIT** — `PUT /videos/{id}` + supporting `GET /videos/{id}`; **land after G-UNIT** so the update
   contract carries `unitId` and is not reworked twice.

> Rationale: Track A is per-video and unaffected by G-UNIT, so it ships first for immediate value.
> Track B is sequenced because G-EDIT's payload depends on the data model G-UNIT establishes.

---

## 2. Gap Register (detailed & implementation-ready)

### G-EDIT — Missing "Update Video" capability  ·  Severity: High  ·  Order: 5

**Provenance:** `[ProdDecision]` — PM-approved; not stated explicitly in PRD.

**Evidence**
- UI: full Edit screen S13 (`762:52798`) — editable name (`762:52831`), unit dropdown (`762:52835`),
  description (`762:52839`), date (`762:52843`), video link (`762:52865`), duration "90 min" (`836:27794`),
  recipients (`997:50931`). Settings row "Edit video" (`990:31988`). Warning modal S14 (`990:32911`).
- Backend: `IVideoService` exposes create / scopes / delete only. No `UpdateVideoAsync`.
  `IVideoAssetRepo` has no update path except `TryUpdateDurationWithinToleranceAsync` (watch trust-boundary).
  Video is effectively immutable after create.

**Proposed contract**
```
PUT /api/videos/{id}
Auth: [Authorize] + [ModulePermission(VideoConstants.ModuleName, <edit-permission>)]   // constant UNKNOWN — confirm
Body: { title, description?, sourceUrl, publishDate?, durationSeconds?, unitId }        // unitId from G-UNIT
Behaviour:
  - Resolve teacherId from JWT; GetVideoByIdAndTeacherAsync → 404 if not owner.
  - Re-parse sourceUrl via IVideoUrlParser if changed (InvalidUrl / UnsupportedSource → 400).
  - Recipients/scopes are NOT edited here — that stays on PUT /videos/{id}/scopes (Story D).
  - Warning modal S14 implies edit may invalidate prior analytics — CONFIRM whether edit resets
    VideoAnalytics or preserves it (product rule, currently UNKNOWN).
Response: { videoAssetId }  (or the full video for immediate re-render)
```

**Supporting endpoint (nested under G-EDIT, not independent):**
```
GET /api/videos/{id}     // teacher/assistant, PermissionView
Returns single-video detail for Edit pre-fill + Overview description:
  { id, title, description, sourceType, sourceUrl, durationSeconds, publishDate?, status?, unitId }
```
> `description` (S7 `762:52755`) is delivered by this supporting GET — it is **not** a standalone missing key.
> Promote `GET /videos/{id}` to an independent gap **only** if a separate product decision defines a
> standalone video-detail screen.

**Target files**
- `Edvanz.API/Controllers/VideosController.cs` (add PUT + GET)
- `Edvanz.Application/ServiceContract/IVideoService.cs` (+`UpdateVideoAsync`, `GetVideoDetailAsync`)
- `Edvanz.Application/Services/VideoService.cs`
- `Edvanz.Application/Dtos/VideoDtos.cs` (+`UpdateVideoRequest`, `VideoDetailDto`)
- `Edvanz.Domain/Interfaces/IVideoAssetRepo.cs` + `Edvanz.Infrastructure/Repositories/VideoAssetRepo.cs`

**Acceptance criteria**
- [ ] `PUT /videos/{id}` updates title/description/sourceUrl/publishDate/durationSeconds/unitId, owner-scoped.
- [ ] Invalid/foreign id → 404; invalid url → 400 with localized key.
- [ ] `GET /videos/{id}` returns pre-fill payload; consumed by Edit screen and Overview description.
- [ ] Product rule on analytics-on-edit (reset vs preserve) confirmed and implemented.

---

### G-ANL — Analytics parity (grouped)  ·  Severity: High  ·  Order: 1–3

All sub-gaps target `GET /api/videos/{id}/analytics` (`VideoService.GetAnalyticsAsync` +
`IVideoAssetRepo.GetAnalyticsRowsForTeacherAsync` / `GetAnalyticsAggregatesAsync`) and the teacher list
row (`GetTeacherVideosPagedAsync`).

#### G-ANL-1 — `completedCount` aggregate missing · High · Order 2 · `[Analytics][PRD-03.4]`
- UI: S7 Statistics "(15)Completed" (`833:26867`); S12 completed list (`926:44394`).
- Backend: response exposes `totalStudentsInScope`, `totalStudentsWatched` only. Per-row
  `estimatedCompletionPct` exists, but **no aggregate completed count** and **no server-side "completed"
  definition/threshold**.
- **Proposed:** add `completedCount` to the aggregates block; define "completed" server-side
  (e.g. `estimatedCompletionPct >= VideoConstants.CompletionThreshold`). Threshold value = **UNKNOWN**, must be product-set.

#### G-ANL-2 — `unseenCount` aggregate not returned · Medium · Order 2 · `[Analytics]`
- UI: S7 "(16)Unseen" (`833:26864`).
- Backend: derivable as `TotalStudentsInScope − TotalStudentsWatched` but not returned explicitly.
- **Proposed:** return `unseenCount` explicitly in aggregates (avoids client re-deriving; keeps one source of truth).

#### G-ANL-3 — list "seen" semantics mismatch · High · Order 1 · `[Analytics]`
- UI: S2/S6 card "20 students **seen**" (`833:26476`), "20 students unseen" (`833:26479`).
- Backend: `GetTeacherVideosPagedAsync` row returns `TotalOpens` (raw open count — increments on every
  re-open) plus `StudentsInScope`. "Distinct students who have seen" is **not** returned; `TotalOpens`
  ≠ distinct-seen.
- **Proposed:** add `seenStudentCount` (distinct) and `unseenStudentCount` to the teacher list row
  projection. Keep `totalOpens` if used elsewhere (currently 🔸 unused on this card).

#### G-ANL-4 — `statusFilter` request param missing · High · Order 3 · `[Analytics][PRD-03.3]`
- UI: three separate screens S10/S11/S12 (Seen / Unseen / Completed) each needing a server-side filter.
- Backend: `VideoAnalyticsRequest` accepts `Search`, `SortBy`, `SortDirection`, `Page`, `PageSize` — **no
  status filter**. All three screens would otherwise share one unfiltered result set filtered client-side.
- **Proposed:** add `statusFilter` enum `{ All, Seen, Unseen, Completed }` (default `All`) to
  `VideoAnalyticsRequest`; apply in `GetAnalyticsRowsForTeacherAsync` WHERE clause using the same
  "completed" definition as G-ANL-1.

**Target files (all G-ANL)**
- `Edvanz.Application/Dtos/VideoDtos.cs` (aggregates block + `VideoAnalyticsRequest.StatusFilter`; teacher row dto)
- `Edvanz.Application/Services/VideoService.cs` (`GetAnalyticsAsync`, `GetTeacherVideosAsync` mapping)
- `Edvanz.Domain/Interfaces/IVideoAssetRepo.cs` + `VideoAssetRepo.cs`
  (`GetAnalyticsAggregatesAsync`, `GetAnalyticsRowsForTeacherAsync`, `GetTeacherVideosPagedAsync`)
- `Edvanz.Domain/Constants/VideoConstants.cs` (`CompletionThreshold`)

**Acceptance criteria**
- [ ] `GET /analytics` aggregates return `totalStudentsInScope, totalStudentsWatched, unseenCount, completedCount`.
- [ ] "Completed" threshold defined once in `VideoConstants` and reused by aggregate + filter.
- [ ] `statusFilter` narrows rows server-side; pagination/totalCount reflect the filtered set.
- [ ] Teacher list row returns distinct `seenStudentCount` + `unseenStudentCount`; "seen" no longer sourced from `totalOpens`.

---

## 3. G-UNIT — Video Unit Model  ·  Severity: Critical  ·  Order: 4 (epic)

**Provenance:** `[ProdDecision]` — product direction confirmed with PM.
**This is not a missing endpoint. It is a Domain-model + Data-model change** that cascades through
entities, migrations, DTOs, endpoints, queries, and UI mapping.

### 3.1 Evidence (UI models a two-level hierarchy)
- S2 "List of videos" items are **units**: "Mathematics - unit 1" (`833:26469`) with child aggregate
  counts — "N videos" (`833:26487`), "0 Quiz" (`833:26491`), attachments (`833:26482`).
- S6 "videos" (`762:52311`) lists videos **inside** a unit ("Introduction" under "Mathematics-unit 1").
- S13 Edit exposes a **Unit dropdown** (`762:52835`) to reassign a video's parent unit.
- S4 "Edit list of videos" (`1008:33212`) edits the **unit** itself (name/description/recipients only).

### 3.2 Current backend reality
`VideoAsset` is **flat**: no `VideoUnit` entity, no `UnitId` FK, no relationship. `GET /videos/teacher`
returns individual videos, not units. There is no place for unit-level counts, unit-level recipients,
or unit edit/delete.

### 3.3 Architectural Impact (cross-cutting — plan as an epic)

| Layer | Change |
|---|---|
| **Domain** | New entity `VideoUnit` (Id, TeacherId, Title, Description, CreatedByUserId, timestamps). `VideoAsset` gains `UnitId` (1:N: Unit → Videos). Decide nullability: is `UnitId` required (every video belongs to a unit) or optional (loose videos allowed)? — **product decision needed**. |
| **Data / EF** | New table `VideoUnits`; FK `VideoAssets.UnitId`. Fluent API config (owner scope, delete behaviour — likely `NoAction`/`Restrict` so deleting a non-empty unit is blocked or cascades per rule). New migration. Backfill strategy for existing videos (assign to a default unit?). |
| **Repository** | Unit CRUD + paged unit list with child aggregates (video/seen/unseen counts per unit). `GetTeacherVideosPagedAsync` re-shaped: list is now **units**, with a drill-down "videos in unit" query. Ownership checks extended to units. |
| **Application / DTOs** | `VideoUnitDto`, `CreateVideoUnitRequest`, `UpdateVideoUnitRequest`; `TeacherVideoListItemDto` split into unit-row vs video-row shapes. Create/Edit video payloads carry `unitId`. |
| **API** | New endpoints: `POST/PUT/DELETE /api/video-units`, `GET /api/video-units` (paged, with aggregates), `GET /api/video-units/{id}/videos`. Existing video endpoints updated to accept/return `unitId`. |
| **Queries** | Unit-level aggregates (count videos, distinct-seen, unseen) — new projections; watch out for N+1 across units. |
| **UI mapping** | S2 → units list; S6 → videos-in-unit; S4 → unit edit; S13 unit dropdown → unit picker; list-card child counts now resolve to real unit aggregates. |
| **Interplay with G-ANL** | Analytics stays **per-video** (unchanged), but unit cards need **unit-rolled-up** seen/unseen/videoCount — a separate aggregation layer above G-ANL. |
| **Interplay with G-EDIT** | `PUT /videos/{id}` must include `unitId`; land G-UNIT first to avoid a second contract change. |

### 3.4 Suggested phasing within the epic
1. Domain + EF: `VideoUnit` entity, `UnitId` FK, migration, backfill rule.
2. Repo + Application: unit CRUD, unit-list-with-aggregates, videos-in-unit.
3. API: unit endpoints; thread `unitId` through create/edit/list.
4. Wire UI mapping (S2/S4/S6/S13) + unit-level aggregate counts.

### 3.5 Open product decisions (blockers)
- [ ] Is `VideoAsset.UnitId` required or optional?
- [ ] Delete behaviour for a non-empty unit (block vs cascade-with-audit)?
- [ ] Backfill: default unit for existing videos, or migration-time assignment?
- [ ] Do unit-level "recipients" (S4) define scope at unit level, or is scope still per-video only?

---

## 4. Consolidation Recommendations (mobile — reduce round-trips)

### C-1 — S7 Overview BFF `GET /api/videos/{id}/overview` · **recommended ✅**
First-paint needs 2–3 calls today: `GET /analytics` (aggregates only) + supporting `GET /videos/{id}` +
a teacher-safe embed URL (currently only from `POST /start`, which is student-only and mutating).

Proposed merged response:
```json
{
  "video": { "id": 0, "title": "", "description": "", "sourceType": "", "embedUrl": "",
             "durationSeconds": 0, "publishDate": null, "status": null, "unitId": 0 },
  "stats": { "totalStudentsInScope": 0, "seenCount": 0, "unseenCount": 0, "completedCount": 0 }
}
```
**Win:** one round-trip on the most-visited detail screen; provides a **teacher preview embed without a
watch mutation**; and doubles as G-EDIT pre-fill (folds the supporting `GET /videos/{id}` in).
**Deliberately excluded:** per-student Seen/Unseen/Completed rows → see C-2.

### C-2 — S10/S11/S12 lists · **keep separate ❌ (do not merge)**
Reached on-tap from S7 and paginated (infinite scroll). Merging into the overview would over-fetch all
students on first paint. Correct design: aggregates in C-1; rows lazy here via `GET /analytics?statusFilter=…&page=…` (G-ANL-4).

### C-3 — S1 Create: `POST /videos` accepting `scopes[]` atomically · **recommended ✅**
Today create is 2 sequential calls (`POST /videos` then `POST /scopes`), leaving an **orphan window**
(video with zero scopes if the second call fails). Proposed: `POST /videos` accepts `scopes[]` and runs
create+append in one transaction. Keep `POST /scopes` for later append (S9 manage-access).

### C-4 — S2 list / S6 videos-in-unit · single call, no merge needed.
### C-5 — S1 "Select Groups" picker · cross-module, on-demand — not part of any VCM aggregate.

---

## 5. Open Questions (NOT gaps — excluded from counts)

Template-bleed (Figma-only, absent from PRD, shared-composer markers):
- **Quiz builder** (S1 `990:32297`, S13 `926:44697`) — reuses Exam/Homework composer.
- **Files / Attachments** tab + upload (S1 `990:32288`, S8 `926:45046`, S13 `835:27029`).
- **Visibility (Published/Draft)** + Settings toggle (S1 `990:32258`, S9 `990:31986`); status badge (S2/S6/S7).
- **Publish date / Date to publish** (S1 `990:32253`, S7 `762:52784`, S13 `762:52843`).
- **List-card extras** — thumbnail (`833:26464`), filter (`833:26524`), quizCount (`833:26491`), attachmentCount (`833:26482`).
- **`sessionName`** on analytics rows (`926:42912`) — not PRD-explicit.

> If any of these later receives a product decision, promote it to a Confirmed Gap (as was done for G-EDIT / G-UNIT).

**N/A to API↔UI:** VCM-FR-04 (screenshot / screen-recording prevention) is a client-side NFR — no
endpoint and no UI element.

---

## 6. Appendix

### 6.1 Backend module surface (identified)
- **Presentation:** `Edvanz.API/Controllers/VideosController.cs` (teacher/assistant),
  `Edvanz.API/Controllers/StudentVideosController.cs` (`[Route("api/videos/student")]`, student role-only).
- **Application:** `IVideoService.cs`, `VideoService.cs`, `VideoDtos.cs` (+ `Dtos.VideoContentManagement`),
  `IVideoUrlParser`, `IVideoScopeResolver` / `VideoScopeResolver.cs`.
- **Domain:** `IVideoAssetRepo.cs` (+ `VideoRepoProjections.cs`); entities `VideoAsset`, `VideoScope`,
  `VideoAnalytics`, `VideoWatchEvent`, `VideoAssetAudit`; `VideoConstants.cs`.
- **Infrastructure:** `Edvanz.Infrastructure/Repositories/VideoAssetRepo.cs`; 5 DbSets on `EdvanzDbContext`.

### 6.2 Endpoint inventory
| # | Method + Path | Handler | Writes |
|---|---|---|---|
| 1 | `POST /api/videos` | `CreateVideoAsync` | INSERT VideoAssets |
| 2 | `POST /api/videos/{id}/scopes` | `AppendScopesAsync` | INSERT-N VideoScopes |
| 3 | `PUT /api/videos/{id}/scopes` | `ReplaceScopesAsync` | DELETE-all + INSERT-N (txn) |
| 4 | `DELETE /api/videos/{id}/scopes/{scopeId}` | `RemoveScopeAsync` | DELETE-1 |
| 5 | `DELETE /api/videos/{id}` | `DeleteVideoAsync` | INSERT audit + cascade DELETE (txn) |
| 6 | `GET /api/videos/teacher` | `GetTeacherVideosAsync` | read-only |
| 7 | `GET /api/videos/student/teachers/{teacherId}` | `GetStudentVideosAsync` | read-only |
| 8 | `POST /api/videos/student/teachers/{teacherId}/{videoAssetId}/start` | `StartWatchAsync` | analytics UPSERT + watch INSERT + conditional duration UPDATE |
| 9 | `POST /api/videos/student/teachers/{teacherId}/{videoAssetId}/stop` | `StopWatchAsync` | analytics atomic UPDATE + watch INSERT |
| 10 | `GET /api/videos/{id}/analytics` | `GetAnalyticsAsync` | read-only |
| 7b | `GET /api/videos/parent` | — | **NOT SHIPPED** (empty region in `IVideoService`) |

> Note: the real student routes carry `/student/teachers/{teacherId}/…`; the `IVideoService` mapping-table
> comments (`GET /api/videos/student`, `POST /api/videos/{id}/start`) are **stale documentation**.

### 6.3 UNKNOWNs / needs repo checkout
- Literal line numbers (project index is chunked, not line-numbered).
- Exact `[ModulePermission]` constant per write endpoint (`PermissionView` confirmed for read/analytics; create/edit/delete constants unconfirmed).
- `StopWatchAsync` body ordering — the scope re-check is inferred from the `IVideoService` error contract (`VideoNotInScope`) + the extracted helpers; exact placement unverified.
- "Completed" threshold value (G-ANL-1/4) — product-set, currently undefined server-side.
- G-UNIT product decisions in §3.5.

### 6.4 Provenance & method notes
- Triggers/navigation in the UI inventory are **INFERENCE** (layer structure + screen adjacency;
  prototype reactions were not read).
- Student-facing screens are **UNKNOWN / Not Designed** in `762:51776` and excluded; no requirements were
  inferred from older sections (e.g. `1:15232`).
