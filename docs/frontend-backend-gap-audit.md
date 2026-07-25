# Frontend ↔ Backend Gap Audit

Gaps between the **mobile app** (Flutter) and the **API** (.NET).
All modules except parent, homework, messaging, notifications, permissions.
Each issue: **Issue** · **API** · **Fix**. Severity: 🔴 must fix · 🟡 should fix · 🟢 minor.

---

## Module 1 — Auth

### A-1 — Forgot Password can't reset the password 🔴
- **Issue:** User enters phone → OTP → then app says "reset not available". No way to reset a forgotten password.
- **API:** `POST api/auth/reset-password` + `api/auth/forgot-password` — app has them, backend doesn't.
- **Fix:** Backend builds reset-password (after OTP). Frontend wires the new-password screen to it.

### A-2 — OTP code is returned in the response and shown on screen 🟢 (leave for now)
- **Issue:** Backend returns the OTP in the response; app shows it. Should go by SMS. On purpose for now — no SMS service yet.
- **API:** `POST api/auth/generate-otp`.
- **Fix:** When SMS is added: backend stops returning it, app removes the on-screen code.

### A-3 — Teacher must make a 2nd call after login to get their own ID 🟡
- **Issue:** Login returns the teacher ID for students/assistants but not for teachers, so the app fetches the profile right after login just to get it.
- **API:** `POST api/auth/login` (no teacher ID) → extra `GET api/Teacher/{id}/profile`.
- **Fix:** Backend adds the teacher's ID to the login response. Tell frontend.

### A-4 — "signup" misspelled as `sigup-by-google` 🟢 (cosmetic)
- **Issue:** Typo in the Google sign-in address. Works (both sides match).
- **API:** `POST api/auth/sigup-by-google`.
- **Fix:** Rename on both sides one day.

---

## Module 2 — Students

### B-1 — "Change session" dropdown never shows on Edit Student 🔴
- **Issue:** The dropdown is built from a session list the app expects in the student response, but the backend sends only the student's current session. So the dropdown stays hidden and the teacher can't move a student.
- **API:** `GET api/teacherstudent/students/{id}` — no session list in the response.
- **Fix:** Backend adds the session list to the response, or frontend loads it from the sessions endpoint.

### B-2 — Student list loads with 2 calls instead of 1 🟡
- **Issue:** App calls list + counts separately, though one endpoint returns both.
- **API:** `students/overview` (unused) vs `students` + `counts` (used now).
- **Fix:** Frontend uses `students/overview`, or removes the dead code. Tell frontend.

### B-3 — Bulk import shows only a success count 🟢 (minor)
- **Issue:** Backend returns the list of imported students; app shows only a number.
- **API:** `POST api/teacherstudent/bulk-import` — `succeeded[]` ignored.
- **Fix:** Frontend can list them if wanted. Optional.

---

## Module 3 — Sessions & Groups

Well aligned. One issue, one note.

### C-1 — Group "description" was thrown away 🟡 ✅ FIXED (backend)
- **Issue:** Create Group forced a description, but the backend had no field for it — it was dropped and never shown.
- **API:** `POST api/session/groups`, `PUT .../groups/{id}`, `GET .../groups`.
- **Fix (done):** Added `Description` to the group (entity + create + rename + response), migration `20260722165401`. App needs no change — its description now saves and returns.

### C-2 — "Ungrouped only" filter unused 🟢 (minor)
- **Issue:** Backend can filter ungrouped sessions (`groupId = -1`); app never sends it.
- **API:** `GET api/session/{teacherId}/sessions?groupId=-1`.
- **Fix:** Frontend can add the filter if wanted. Optional.

---

## Module 4 — Attendance

Request bodies (mark/bulk/edit/add/delete), status/method values, and the student `/month` + `/summary` routes all match. Issues are on the response side.

### D-1 — Edit/delete makes an extra call to find the record id 🟡
- **Issue:** To edit or delete a mark, the app needs its record id — but the day's student list doesn't include it. Even the mark response has it (nested under `record`), but the app reads only top-level `attendanceRecordId`/`recordId`/`id`, so it misses it there too. So the app makes a second call to another endpoint to look the id up. (It also has a dead fallback that scans up to 100 pages that never have the id.)
- **API:** `GET api/Attendance/sessions/{id}/students?occurrenceDate=` (no record id) → extra `GET .../occurrences/{date}/students` (has ids). Also `POST api/Attendance/mark` returns `record.id` (app doesn't read it).
- **Fix:** Backend adds `attendanceRecordId` to each student in the list (null if unmarked); frontend also reads `record.id` from the mark response. Then no extra call, and the scan can be deleted.

### D-2 — Absence numbers don't show (app reads the wrong key) 🟡
- **Issue:** Backend sends absences as numbers; app looks for a text label that isn't sent, so it's always empty.
- **API:** `GET api/Attendance/sessions/{id}/students`.
- **Backend sends but app ignores:** `consecutiveAbsences`, `totalAbsences`, `isMarked`, `isHeld`, `barcode`.
- **App looks for but backend never sends:** `absenceConsequenceLabel` (label text), `wasAbsentLastSession`, `packageSummary`, `profileImageUrl`.
- **Fix:** Frontend reads `consecutiveAbsences`/`totalAbsences` (and `isHeld` if the "held" state is shown) and builds the label itself; drop the fields the backend doesn't send.

### D-3 — "Mark all" result is thrown away 🟡
- **Issue:** Backend returns a full result; the app treats mark-all as returning nothing, so it can't show skips or alerts.
- **API:** `POST api/Attendance/mark-bulk`.
- **Backend sends but app ignores:** `successCount`, `skippedCount`, `absenceAlertCount`, `absenceAlerts[]`, `totalPresent`, `totalAbsent`, `results[]` (per-student outcome).
- **Fix:** Frontend parses the full result: show `successCount`/`skippedCount`, reconcile each student from `results[]` (mark which were saved vs skipped), and raise the absence-alert prompt for every student in `absenceAlerts[]`.

### D-4 — App guesses field names instead of using the real one 🟢 (minor; root cause of D-2 and D-5)
- **Issue:** For each field the app tries a list of guessed names and takes the first that exists. When the real name isn't in the list, the field is silently empty (this is how D-2 and D-5 slipped through).
- **API:** all attendance responses.
- **Fix — drop the guesses, use the backend key:**

| Field | App guesses (wrong) | Use this |
|---|---|---|
| status | `status`, `attendanceStatus`, `currentStatus` | `currentStatus` |
| record id | `attendanceRecordId`, `recordId`, `id` | `record.id` |
| absence alert | `requiresAbsenceAlertConfirmation`, `requiresAbsenceAlert` | `hasAbsenceAlert` |
| absences | `absenceConsequenceLabel` (text) | `consecutiveAbsences`, `totalAbsences` (numbers) |
| absent last session | `wasAbsentLastSession`, `requiresAbsenceAlert`, `absentLastSession` | not sent — remove |
| profile image | `profileImageUrl`, `avatarUrl` | not sent — remove |
| package summary | `packageSummary`, `enrollmentSummary` | not sent — remove |

### D-5 — After marking, the absence-alert prompt never fires (wrong key) 🟡
- **Issue:** When a student was absent last session, the mark response tells the app to show a confirm prompt via `hasAbsenceAlert`. The app reads `requiresAbsenceAlertConfirmation`/`requiresAbsenceAlert` instead — neither is sent — so the flag is always false and the prompt never appears (REQ-ATT-028/058).
- **API:** `POST api/Attendance/mark`.
- **Backend sends but app ignores:** `hasAbsenceAlert`, `consecutiveAbsences`, `lastAbsenceDate`, `lastAbsenceSessionName`, `lastAbsenceWasCrossSession`, `duplicateSessionName`, `duplicateRecordedAt`, `assignedSessionId`, `assignedSessionName` (app uses only `isDuplicate`).
- **Fix:** Frontend reads `hasAbsenceAlert` to trigger the prompt, fills it with `consecutiveAbsences` + `lastAbsenceDate` + `lastAbsenceSessionName` + `lastAbsenceWasCrossSession`, and on a duplicate shows `duplicateSessionName`/`duplicateRecordedAt` and the assigned session from `assignedSessionId`/`assignedSessionName`.

---

## Module 5 — Payments

Endpoints all line up (v1 tracking / students / collections / collect-students / lookup / mark-paid / submit / wallet + withdraw; legacy `api/Payment/*`; student tracking). Query params match (`month`, `year`, `page`, `limit`, `filter`, `search`, `status`, `qr`/`code`/`name`). One real bug and a couple of notes.

### E-1 — Collecting a payment fails from the app: student ids sent as text, backend needs numbers 🔴
- **Issue:** The collect/lookup responses return `studentId` as **text** (a string). The app keeps it as text and sends it straight back when submitting a collection or marking paid. But those request bodies require the id as a **number**, and the backend rejects a quoted text value where a number is expected (strict JSON) — so the request fails with 400. `classSessionId` has the same problem (sent as text, backend wants a number).
- **API:** `POST api/v1/collect/submit` (`students[].studentId`, `classSessionId`) and `POST api/v1/payments/collect/mark-paid` (`studentIds[]`) — backend types are `long` / `long?`, app sends strings.
- **Fix:** Send numeric ids. Frontend converts `studentId` (and `classSessionId`) to a number before sending in submit and mark-paid. To stop it recurring, backend should also return `studentId` as a number in the collect/lookup/tracking responses (today it returns a string), so both sides use one numeric id type.

### E-2 — Two wallet surfaces are both in use 🟢 (note)
- **Issue:** The app uses `api/v1/assistants/{id}/wallet` (detail + withdraw) and the legacy `api/Payment/wallets` (list). Both exist and work; just worth knowing there are two wallet APIs.
- **API:** `GET api/v1/assistants/{id}/wallet`, `GET api/Payment/wallets`.
- **Fix:** None needed. Consolidate onto one surface later if desired.

**Clean:** tracking/students/collections/lookup/withdraw request + response shapes, legacy `api/Payment/*` (payment-view, history, wallets, reset), and student tracking `api/payment/student/teachers/{id}/tracking` all match.

---

## Module 6 — Videos / VCM

The **student side is fully in sync** (units, unit videos, watch start/stop, exam + submit/retry/result/review all match). The **teacher write side is out of date** — the app already adopted file ids (it sends `VideoPhotoFileId`, `AttachmentFileIds[]`, etc.) but still sends them over the **wrong transport** (multipart form) and calls some routes that were since removed.

> **Verified against prod's live swagger.** `POST /api/videos`, `PUT /api/videos/{id}`, `PATCH .../status`, and `PUT .../video-photo` all accept **`application/json` only**; `.../status/toggle`, `.../units`, and `.../attachments/{id}/download` are **not present**. The app sends `multipart/form-data` (forced by its own `ApiService` interceptor). So by the contracts create/update should return **415**.
>
> **Caveat:** create is reported working in the app. I could not run a live create against prod to confirm (it's a prod write). The likely explanation is the tested app was hitting an **older backend build** that still used form binding — in which case these break when the current JSON build is deployed. Worth a quick end-to-end check on the target environment.

### F-1 — Creating a video sends multipart, backend accepts JSON only 🔴
- **Issue:** The app already uploads files first and puts their ids in the body (`VideoPhotoFileId`, `AttachmentFileIds[]`, `Exam.Questions[].ImageFileId`) — so the upload step is fine. The problem is only the **transport**: it sends the body as `multipart/form-data`, but the endpoint accepts `application/json` only, so the request is rejected (415).
- **API:** `POST api/videos` — backend `CreateVideoRequest` is JSON; app sends multipart.
- **Fix:** Send the **same fields as a JSON body** (not multipart). No change to the upload flow — the app already has the file ids.

### F-2 — Editing a video sends multipart, backend accepts JSON only 🔴
- **Issue:** Same transport problem as F-1 for update.
- **API:** `PUT api/videos/{id}` — backend `UpdateVideoRequest` is JSON; app sends multipart.
- **Fix:** Send the update as a JSON body (same fields, just JSON instead of multipart).

### F-3 — Show/hide a video hits a non-existent endpoint 🔴
- **Issue:** To toggle a video's visibility the app calls `.../status/toggle`, which doesn't exist. The backend has `PATCH .../status` where you send the desired status.
- **API:** app calls `PATCH api/videos/{id}/status/toggle` (404); backend has `PATCH api/videos/{id}/status`.
- **Fix:** App calls `PATCH api/videos/{id}/status` with the target status in the body (its other status call already uses this route).

### F-4 — Assigning a video to units hits a non-existent endpoint 🔴
- **Issue:** The app calls `PUT api/videos/{id}/units` with `unitIds`. There is no such route. Unit membership is set through the video create/update body (with the scope endpoints for scoping).
- **API:** app calls `PUT api/videos/{id}/units` (404); backend has `POST/PUT api/videos/{id}/scopes` and sets units via create/update.
- **Fix:** App sends unit ids in the video create/update JSON body (F-1/F-2), or the backend adds a dedicated assign-units endpoint if a standalone action is needed.

### F-5 — Downloading a video attachment hits a removed endpoint 🔴
- **Issue:** The app calls the old attachment-download endpoint, which was removed when files moved to the gated file store. Attachments are now read through the gated file URL.
- **API:** app calls `GET api/videos/{id}/attachments/{aid}/download` (removed); files are served by `GET api/files/{fileId}`.
- **Fix:** App uses the attachment's `fileId` from the video response and fetches it via `GET api/files/{fileId}`.

**Clean:** all student endpoints (`api/videos/student/...`), teacher unit CRUD (`api/video-units`), unit videos, video detail/overview/analytics reads, and `PATCH .../status` (route + verb).

---

## Module 7 — Offline Exams

All endpoints match, and **create/update are fully in sync** with the current per-session contract (recipient `sessionIds` XOR `groupIds`; `examDate` for SeparateTime; `sessionOccurrences: [{sessionId, sessionOccurrenceId}]` for DuringSession; delivery values `DuringSession`/`SeparateTime`). Verified against prod's live swagger. Two real bugs on the grade/attendance write path, plus one pagination gap.

### G-1 — Saving exam grades uses the wrong student key (grades don't save) 🔴
- **Issue:** The grade-save item sends the student as `obligationId`, but the backend expects `teacherStudentId`. Since `obligationId` isn't a field the backend reads, the student id binds to 0 and the grade can't be attributed. The roster row already returns **both** ids, so the app has the right value — it's sending the wrong one.
- **API:** `PUT api/exams/grades` — `items[].teacherStudentId` expected (verified on prod: `GradeItemDto` = `teacherStudentId`, `grade`, `rowVersion`); app sends `items[].obligationId`.
- **Fix:** App sends `teacherStudentId` as the item key instead of `obligationId`; keep `grade` and `rowVersion`.

### G-2 — Marking exam attendance uses the wrong student key 🔴
- **Issue:** Same as G-1 for exam attendance — the item sends `obligationId`, backend expects `teacherStudentId`.
- **API:** `PUT api/exams/attendance` — backend `ExamAttendanceItemDto` = `teacherStudentId`, `present`; app sends `obligationId`, `present`.
- **Fix:** App sends `teacherStudentId` (from the roster row) instead of `obligationId`.

### G-3 — Exam home only loads the first page 🟡
- **Issue:** The exam home screen requests no pagination, so the backend returns only page 1 of upcoming and page 1 of past (20 each). A teacher with more than 20 upcoming or 20 past exams can't reach the rest.
- **API:** `GET api/exams/home` (accepts `upcomingPage`, `pastPage`, `pageSize`; app sends none).
- **Fix:** App sends `upcomingPage`/`pastPage`/`pageSize` and paginates both lists.

**Clean:** create/update (per-session occurrences), delete (`?confirm`), `session-dates` (`sessionId`/`year`/`month`), exam detail, session roster (`page`/`pageSize`/`search`), scan (`occurrenceId`/`code`), and the student offline-exams endpoint (`api/assignmentobligations/student/teachers/{id}/exams`).

---

## Module 8 — Online Exams

**Clean — no gaps found.** Verified every route and request body against prod's live swagger. Both teacher and student sides line up: JSON bodies throughout (unlike videos), correct keys, and the file-registry `imageFileId` used correctly on questions.

- **Endpoints (all match):** list/create `api/online-exams`; get/update/delete `{id}`; `overview`; `scope-analysis`; `PATCH status`; questions `GET/POST/PUT` + `questions/bulk` + `questions/overview`; `PATCH students/{teacherStudentId}/status`; student `student/teachers/{id}` + `questions` + `answers` (GET/POST) + `submit` + `result` + `block`.
- **Bodies (all match):** create/update (`title`, `description`, `instructions`, `startDateTime`, `endDateTime`, `passPercentage`, `visibility`, `scopes[{scopeType, sessionId, sessionGroupId}]`, `questions`); question (`questionText`, `questionType`, `degree`, `imageFileId`, `options[{optionText, isCorrect}]`); status (`status` + `rowVersion`); student status (`status`); student answers (`questionId`, `selectedOptionIds`); submit (`{answers:[…]}`).

### H-1 — App sends an extra `status` field on create 🟢 (no-op)
- **Issue:** The create body includes a `status` field that `CreateOnlineExamRequest` doesn't define. The backend ignores unknown fields, so the exam is still created as Draft (its default) — harmless.
- **API:** `POST api/online-exams`.
- **Fix:** None needed; frontend can drop the extra `status` field for tidiness.

---

## Module 9 — Assistants

**Clean — no real gaps.** All endpoints, HTTP verbs, request bodies, and response keys match.

- **Endpoints/verbs (all match):** `GET/POST api/assistant`, `GET/PUT api/assistant/{id}`, `PATCH .../activate`, `PATCH .../deactivate`, `PATCH .../delete`, `GET .../login-activity`.
- **Create/update bodies match:** `fullName`, `username`, `password`/`newPassword`, `email`, `phoneNumber`, `permissionProfileIds`, `permissionIds`, `teacherId`.
- **Responses match:** list (`isActive`, `accountStatus`, `teacherName`, …); detail has no `isActive` but the app correctly **derives** it from `accountStatus`; login-activity (`occurredAt`, `action`, `deviceOrBrowser`, `ipAddress`) — the app reads `deviceOrBrowser` correctly.

### I-1 — Shared typo `isAcitve` in the list filter 🟢 (cosmetic)
- **Issue:** The active/inactive list filter is spelled `isAcitve` on **both** the app and the backend (`AssistantPerTeacherFilterDto`). It works because both sides match — just misspelled.
- **API:** `GET api/assistant?isAcitve=`.
- **Fix:** Rename to `isActive` on both sides together one day.

---

## Module 10 — Subscription & Teacher Config/Profile

Teacher **profile** and **configuration** GET/PUT are wired and mostly match. **Subscription is not wired at all** on the app, and config save silently resets a few fields.

### J-1 — The whole subscription feature is not connected to the backend 🟡
- **Issue:** The subscription screen is a static stub — it shows a "subscription unavailable" toast and a hardcoded plan UI, and makes **no** API calls. The backend has a full subscription API that the app never uses: current status, history, renewal (initiate / manual-submit / status), and capacity-increase requests (create/list/cancel). So from the app a teacher can't see their real subscription, renew, or request a capacity increase.
- **API (all unused by the app):** `GET api/subscription/current`, `GET api/subscription/history`, `POST api/subscription/renew/initiate`, `POST api/subscription/renew/manual-submit`, `GET api/subscription/renew/status/{id}`, `POST/GET api/subscription/capacity-requests`, `DELETE api/subscription/capacity-requests/{id}`, plus `GET api/teacher/subscription` and `GET api/teacher/capacity-packages` (the app defines the `capacity-packages` constant but never calls it).
- **Fix:** Frontend builds the subscription integration against these endpoints (current + renew + capacity-requests). Backend is ready.

### J-2 — Saving settings silently resets 3 config fields to defaults 🟡
- **Issue:** The config screen sends most fields, but **omits** three the backend PUT still has with non-null defaults. Because the app doesn't send them, each save writes the defaults over whatever was there:
  - `studentVisibilityVideo` → reset to `true` (the app has no video-visibility toggle at all, and every save re-enables it),
  - `studentCodeLanguage` → reset to `English`,
  - `sessionNameLanguage` → reset to `English` (so an Arabic-generation teacher loses it on any settings save).
- **API:** `PUT api/Teacher/{id}/configuration` — app omits `studentVisibilityVideo`, `studentCodeLanguage`, `sessionNameLanguage`; the backend `UpdateTeacherConfigurationDto` defaults them to `true`/`English`/`English`.
- **Fix:** Frontend round-trips all config fields it received on GET (including these three) so a save preserves them, and adds the student video-visibility toggle. (Optionally the backend makes the PUT patch-style — omitted field = unchanged — to prevent this class of clobbering.)

**Clean:** teacher profile GET/PUT and the config fields the app does send (`studentCodeGenerationMode`, `sessionNameMode`, `isProratedPaymentEnabled`, `proratedTiers`, `consecutiveAbsenceThreshold`, `consecutiveUnpaidThreshold`, `barcodeDisplayMode`, and the attendance/payment/homework/exam student + parent visibility flags).

---

## Module 11 — Student Linking & Teacher-Home Aggregate

**Clean — no gaps found.** Verified all request bodies and the home response against prod's live swagger.

- **Student side (`api/studentuser/me/*`):** `dashboard`, `teachers` (parses `teacherId`/`linkId`/`status`/`teacherCode`/`isLinked`), `link-requests` POST (`teacherCode`, `studentName`, `studentCode`), cancel/unlink `DELETE me/teachers/{teacherId}`, `teachers/{id}/home`, `teachers/{id}/barcode` — all match.
- **Teacher side (`api/teacher/student-links/*`):** `my-code`, `requests`, `accept` (`teacherStudentId`/`studentCode`), `reject`, linked list, `remove` (`linkIds`), `bind` / `unbind` (`teacherStudentId`/`studentCode`) — all match, including the "accept & link" dual-key body.
- **Teacher-home aggregate:** the app reads every top-level key the backend sends — `teacherId`, `teacherName`, `subjectName`, `teacherCode`, `linkStatus`, `isLinked`, `month`, `monthLabel`, and the `attendance`/`payment`/`videos`/`homework`/`exams` sections. (The parser also keeps defensive fallbacks like D-4, but the primary keys match, so nothing is silently empty here.)

---

## Summary (Modules 1–11)

**Critical (🔴) — a feature is broken/blocked:**
- A-1 Forgot Password has no backend reset endpoint *(backend)*
- B-1 Edit-Student session dropdown never shows *(backend response missing the list)*
- E-1 Payment collection sends student ids as text, backend needs numbers *(frontend)*
- F-1…F-5 Video authoring: multipart instead of JSON + removed endpoints *(frontend)*
- G-1/G-2 Exam grade + attendance save use `obligationId` instead of `teacherStudentId` *(frontend)*

**Should fix (🟡):** A-3 (teacher id in login), D-1…D-3/D-5 (attendance response keys/results), G-3 (exam-home pagination), J-1 (subscription not wired), J-2 (settings save resets 3 fields).

**Minor (🟢):** A-2 (OTP, by design), A-4 / I-1 (shared typos), B-3, C-2, D-4, E-2, H-1.

**Fixed:** C-1 (group description — backend, shipped).

**Clean modules:** Sessions (after C-1), Online Exams, Assistants, Student Linking.

**Ownership pattern:** the backend is largely current; the app has drifted behind on several write paths (Videos, Exams grade/attendance, Payments) and hasn't wired Subscription. Backend's own gaps are few and additive (build reset-password; add teacher-id/session-list/record-id to existing responses).
