# Gap-Fixes Phase-3 Prod Test Report — 2026-07-18

Live prod verification of commit `70c2721` (Figma↔API gap fixes: in-app QR barcode,
student video-quiz flow, online-exam self-block + Subject, offline-exam list enrichment).
Supersedes the deleted `docs/gap-fixes-handoff-2026-07-18.md` (Phases 1–2 are recorded in
that commit's message and in project memory).

**Server:** `https://app-edvanz-api-prod.azurewebsites.net`
**Rig:** teacher `omran2` (teacherId 14, sessions 41/42) + student account `student1`
(Youssef Tarek, `STU000001`) linked via the request/approval flow (linkId 14) and bound to
roster student **TS 774** ("A2 Test", code `A3`, session 42).

**Result: 41/41 scenarios PASS across two rounds (29 core + 12 edge). 1 UX/contract
finding (not a regression) + 1 minor observation, listed at the end. Message/code audit
in §7: all 16 new resx keys verified live in EN and AR.**

---

## 1. Barcode / in-app QR (B-series) — PASS

| # | Scenario | Result |
|---|---|---|
| B1 | `GET /api/studentuser/me/teachers/14/barcode` linked+bound | 200 — `code:"A3"` (per-teacher roster code, NOT the global account code), teacher name, real ZXing QR SVG |
| B2 | Barcode for unlinked teacher (13) | 403 `StudentNotLinkedToTeacher` |
| B3 | Teacher config `BarcodeDisplayMode=HardCopyOnly` | 403 `BarcodeNotAvailableInApp`; restored to `InApp` afterwards |
| B4 | Scan-loop closure: `POST /api/assignmentobligations/occurrences/43/scan` | `A3` → `ScanAlreadyProcessed` (resolved), `A2` → `ScanRecorded` (marked 772 attended), `ZZZ99` → 404 `BarcodeNotFound`. The QR payload (StudentCode) resolves end-to-end through the existing scan path |

## 2. Student video quiz (V-series) — PASS

Fixture: video 39 (quiz `videoExamId` 8: Q11 SingleChoice, Q12 MultipleChoice, degree 1 each).

| # | Scenario | Result |
|---|---|---|
| V2 | List enrichment `GET /api/videos/student/teachers/14` | `hasQuiz`, `questionsCount`, `watchStatus`, `subject:"English Language"`, `videoPhotoUrl`, `attachments` on every row |
| V3 | Take-screen | 200; **no `isCorrect` anywhere in raw JSON**; status null / canRetake false on first visit; gated question-image URL 302s for the scoped student |
| V4 | Submit (Q11 correct; Q12 one-of-two correct) | Exactly per grader spec: score 1.5/2, 75.00%, 4 stars, correct 1, wrong 1 |
| V5 | Double submit | 409 `VideoQuizAlreadySubmitted` |
| V6 | Result | 200, persisted stats |
| V7 | Review (finalized) | key revealed (`isCorrect`), `awardedDegree` 1.0 / 0.5, `isSelected` accurate |
| V8 | Retry | 200 `VideoQuizReset` → InProgress; review afterwards hides key (0 leaks, awardedDegree null); post-finalize take-screen had shown `canRetake:true, lastPercentage:75` |
| V9 | Resubmit all-correct | 100%, 5 stars, 2/2 |
| V10 | Units views | student sees unit 4 with `videoCount:2` (correctly filtered vs teacher's 4), `quizCount:2`, subject; per-unit video list enriched |
| V11 | Video without quiz | 404 `VideoQuizNotFound` |
| V12 | Video out of scope (session 41 only) | 403 `VideoNotInScope` |
| V13 | Submit validation | two options on SingleChoice → 400 `VideoQuizSelectOneAnswer`; foreign optionId → 400 `VideoQuizInvalidOptionSelection`; typo'd JSON field → 400 (unmapped-member disallow) |
| V14 | `watchStatus` transition | NotStarted → InProgress after start/stop (200s of 1320s) |

## 3. Online exams (O-series) — PASS

Probe exams 31 (2Q) and 32 (1Q), scoped session 42, published, window open. Deleted after test.

| # | Scenario | Result |
|---|---|---|
| O1 | Self-block mid-exam (31) | 200 `OnlineExam.ExamBlocked` + stats (lazy-created report); repeat → 200 `OnlineExam.AlreadyBlocked` (idempotent); submit after block → 409 `OnlineExam.StudentBlocked` |
| O2 | Block after finalize (32) | answer-by-answer save + finalize both 200, then block → 409 `OnlineExam.ExamAlreadyFinalized` |
| O3 | Duration computed from window | list `duration` = End−Start (e.g. `2.23:59:00`); take-screen carries only Start/End (frontend-driven countdown, as designed) |
| O4 | Subject on list | `subject:"English Language"` on every row (upcoming + past); finalized exam shows `studentDegree`/`studentStatus:"Passed"` |
| O5 | No key leak on questions | `isCorrect` absent from student take shape |

Note: the S4 `/answers` endpoint takes a **single** `{questionId, selectedOptionIds}` body
(bulk `{answers:[...]}` belongs to `/submit` only). Sending the wrong wrapper yields a
confusing 404 `OnlineExam.QuestionNotFound` (unmapped body → questionId 0) because this DTO
doesn't disallow unknown members — instance of the deferred global CC-2 issue, not new.

## 4. Offline exams list enrichment (F-series) — PASS

`GET /api/assignmentobligations/student/teachers/14/exams` for TS 774:

- `maxGrade` (60/50/25) and `subject` on every row.
- `rank`/`groupSize` null while ungraded/merely-Attended (by design — cohort is
  `AttendedWithGrade` only).
- After grading occ 43 (774→18/25, 772→22/25): row shows `score:18, scorePercentage:72.0,
  rank:2, groupSize:2, status:"AttendedWithGrade"` — exact competition ranking.

## 4b. Round 2 — edge cases (E-series) — PASS

| # | Scenario | Result |
|---|---|---|
| E1 | Teacher token on a student-only route | 403 localized "You are not authorized to perform this action" + `roleNotAllowed:true` |
| E2 | Link Active but UNBOUND (`unbind` link 14, then re-bind) | barcode → 403 `StudentNotBoundToRoster` (localized); video-quiz take → 403 raw `StudentEnrollmentRemoved` (see §7 finding) |
| E3 | Quiz result with no attempt ever (video 38) | 404 `VideoQuizAttemptNotFound` "You haven't taken this quiz yet." |
| E4 | Submit answer for a question not in the quiz (id 999) | 400 `VideoQuizQuestionNotFound` |
| E5 | Multi-choice answer with empty `selectedOptionIds` | 200 — counted as **notAnswered** (not wrong), 0% |
| E6 | Submit with empty `answers:[]` | 200 — finalizes at 0%, notAnswered 2 (documented "any subset" semantics) |
| E7 | Retry → all-correct after the 0% attempts | 100%, 5 stars (state machine intact) |
| E8 | DRAFT video with quiz, scoped to the student's session | take → 403 `VideoNotInScope` (Published gate holds; no draft leak) |
| E9 | Self-block on a DRAFT exam | 404 `OnlineExam.NotFound` (no existence leak) |
| E10 | Self-block on a published exam scoped to another session | 403 `OnlineExam.NotInScope` "You're not assigned to this exam" |
| E11 | Self-block on a published exam whose window ENDED | 200 `ExamBlocked` — succeeds (observation §6: block skips window validation; own-report only, harmless) |
| E12 | Offline rank tie (both graded 22/25) | both rank 1, groupSize 2 (competition ranking); grade amend 22→18 restores rank 2 |

All E-series probe fixtures (video 51, exams 33/35) deleted after the run.

## 5. Finding (open) — Blocked online-exam state invisible to the student read surface

Server enforcement is correct (all writes 409), but after a block:

- the list keeps the exam in **`upcoming` with `studentStatus:null`** (the `finalized`
  predicate is `SubmittedAt != null`, and blocks don't stamp it);
- the take-screen (`/questions`) returns 200 with full questions and **no attempt-state field**;
- `/result` returns 200 with all-zero stats (indistinguishable from not-attempted).

So after an app restart the frontend has no way to render "you are blocked" — the student
can answer everything again and only discovers the block when submit 409s. Applies to both
the new O1 self-block and the pre-existing teacher T5s block. Recommended minimal fix
(needs frontend sign-off — contract addition): treat `Blocked` like finalized in the list
projection (`StudentOnlineExamService.GetMyExamsAsync` — surface `studentStatus:"Blocked"`,
bucket into `past`), and/or add a status field to the take-screen/result DTOs.

## 5b. Observation (minor) — self-block skips window validation

Blocking succeeds (200 `ExamBlocked`) on a published exam whose window already ended,
lazily creating a Blocked report for an exam the student could no longer take anyway.
Own-report only, no security impact; arguably should 409 like submit does.

## 6. Prod fixture state after this run

- Link **student1 ↔ omran2** (linkId 14) left **Active, bound to TS 774** — reusable for
  future student-side tests (student1 remains linked to teacher1 as before).
- omran2 config untouched (`BarcodeDisplayMode` restored to `InApp`).
- Video 39: quiz attempt history for TS 774 (Completed, 100%) + watch progress (InProgress).
- Exam occurrence 43: 772 marked attended (by scan) and graded 22/25; 774 graded 18/25.
- Probe fixtures deleted: videos 49/50, online exams 31/32.

## 7. Message & return-code audit (are all messages clear to the user?)

All **16 new resx keys** from `70c2721` exist 1:1 in `Messages.en.resx` and
`Messages.ar.resx` (natural Egyptian Arabic), carry stable machine codes on the wire, and
were exercised live. `Accept-Language: ar` verified returning the Arabic text (e.g.
`VideoQuizAlreadySubmitted` → "إنت خلصت الاختبار ده خلاص. دوس إعادة عشان تحله تاني.").

| Code (on the wire) | HTTP | EN message | Verdict |
|---|---|---|---|
| `StudentNotLinkedToTeacher` | 403 | You're not linked to this teacher, so there's no code to show. | clear |
| `StudentNotBoundToRoster` | 403 | Your account isn't connected to a student record with this teacher yet… | clear |
| `BarcodeNotAvailableInApp` | 403 | This teacher issues attendance codes as printed cards only. | clear |
| `OfflineExamsRetrieved` | 200 | Offline exams retrieved successfully. | clear |
| `OnlineExam.QuestionNotFound` | 404 | Question not found. | clear |
| `OnlineExam.ExamBlocked` | 200 | You've left the exam, so it's now locked for you. | clear |
| `OnlineExam.AlreadyBlocked` | 200 | You're already blocked from this exam. | clear |
| `OnlineExam.ExamAlreadyFinalized` | 409 | You've already submitted this exam, so it can't be blocked. | clear |
| `VideoQuizSubmitted` | 200 | Your quiz was submitted. | clear |
| `VideoQuizReset` | 200 | You can take the quiz again. | clear |
| `VideoQuizNotFound` | 404 | This video has no quiz. | clear |
| `VideoQuizQuestionNotFound` | 400 | One of the answers is for a question not in this quiz. | clear |
| `VideoQuizInvalidOptionSelection` | 400 | One of the selected answers isn't valid for its question. | clear |
| `VideoQuizSelectOneAnswer` | 400 | Choose one answer only for this question. | clear |
| `VideoQuizAlreadySubmitted` | 409 | You already submitted this quiz. Tap Retry to take it again. | clear |
| `VideoQuizAttemptNotFound` | 404 | You haven't taken this quiz yet. | clear |

Related codes verified on the same routes: `VideoNotInScope` (403), `OnlineExam.NotFound`
(404), `OnlineExam.NotInScope` (403), `OnlineExam.StudentBlocked` (409), `ScanRecorded` /
`ScanAlreadyProcessed` (200), `BarcodeNotFound` (404), `LinkBound`/`LinkUnbound` (200) —
all localized with codes.

**The one NOT-clear surface (pre-existing, shared):** the controller-level
JWT→TeacherStudent resolution helper, duplicated verbatim in 6 student controllers
(videos, video-exams, online-exams, assignment-obligations, attendance, payment), replies

```json
{ "success": false, "message": "TeacherLinkNotFound" }
```

— the raw resx KEY as the message, **no `code` field, never localized** (both resx files DO
contain translations for `StudentUserNotFound` / `TeacherLinkNotFound` /
`StudentEnrollmentRemoved`; the helper just never calls the localizer). An Arabic-language
user sees the bare English key. The barcode endpoint (service-layer resolution) shows the
correct pattern. Fix = run those three through `IStringLocalizer` and add the `code` field
(or centralize the helper into the base controller) — mechanical, 6 files.
