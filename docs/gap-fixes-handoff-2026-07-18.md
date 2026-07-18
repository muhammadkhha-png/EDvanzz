# Gap Fixes 2026-07-18 — Handoff (Figma↔API audit → fixes → prod verification)

Recreated 2026-07-18 (the original untracked handoff was deleted from disk before testing;
this version folds in the completed test results). Companion doc:
**`docs/gap-fixes-phase3-test-report-2026-07-18.md`** — the full live-prod scenario matrix.

## 1. What shipped (commit `70c2721`, master_integration, LIVE on prod)

Figma design (fileKey `iTrdNauoIhcO3vwSUHB7wP`, page "ui") was audited against the API for
4 student-facing modules; every actionable gap was implemented in one commit:

| Area | Delivered |
|---|---|
| Barcode/QR (B1–B3) | `GET /api/studentuser/me/teachers/{teacherId}/barcode` — config-gated (`TeacherConfiguration.BarcodeDisplayMode == InApp`), per-teacher code (`TeacherStudent.StudentCode`), ZXing QR SVG encoding that code so the existing scan endpoints resolve it. Code-128 hard-copy path untouched. |
| Video quiz (V1–V4) | `StudentVideoExam{Report,Answer,AnswerOption}` (+ additive migration `20260717235511_StudentVideoExamReport`), take/submit/retry/result/review under `api/videos/student/teachers/{tid}/videos/{vid}/exam*`, student units view (`.../units[/{unitId}/videos]`), list enrichment (`hasQuiz`, `questionsCount`, `watchStatus`, `subject`). Retake is video-quiz ONLY. |
| Online exams | Frontend-triggered self-block `POST /api/online-exams/student/teachers/{tid}/{examId}/block` (O1); `subject` on list rows; grader extracted to shared `OnlineExamGradingService` (scores byte-identical); O3 = duration computed from Start/End window, no server timer field. |
| Offline exams (F1–F3) | Student list rows (`GET /api/assignmentobligations/student/teachers/{tid}/exams`) enriched with `maxGrade` (occurrence snapshot), `subject`, `rank`/`groupSize` (competition ranking over the occurrence's `AttendedWithGrade` cohort). |

Cross-cutting: batched gated-file URL resolution (no N+1), two race→409 guards, grader
empty-set hardening, subject unified on stored `LanguagePreference`, 16 new resx keys
(EN + Egyptian AR, verified 1:1).

## 2. Decisions (do not re-litigate)

- Barcode in-app ONLY when `BarcodeDisplayMode==InApp`; one code per linked teacher; QR
  encodes the roster `StudentCode` (NOT the global `StudentAccountCode`).
- O1 block is frontend-triggered (no server auto-detect). Online exams have NO retake.
- Retake exists for VIDEO quizzes only (`POST .../exam/retry`).
- MCQ grading unchanged: single = all-or-nothing; multi = `degree × max(0,(C−W)/|C|)`, 2dp;
  stars 90/75/60/40 thresholds.
- O3 timer: client computes from `StartDateTime`/`EndDateTime`; window end is the only hard stop.
- Offline rank cohort = per-session occurrence (one-line change in
  `ExamHomeworkRepo.GetStudentExamRanksAsync` if group-wide leaderboards are ever wanted).

## 3. Verification status — COMPLETE (2026-07-18, live prod)

Two rounds, 41 scenarios total, **all passing** — happy paths, negatives, and edge cases
(unbound link, draft/published gates, foreign ids, empty submissions, rank ties, past-window
block, role gates, AR localization). Full matrix in the test report. All 16 new message
keys exercised live in EN, spot-checked in AR (`Accept-Language: ar`).

Standing prod fixtures (test tenant omran2, teacherId 14): student1 (`STU000001`) linked
(linkId 14) and bound to TS 774; video 39 = quiz video; exam occurrence 43 graded
(774: 18/25 rank 2, 772: 22/25 rank 1). All probe fixtures were deleted.

## 4. Open findings (recorded, NOT fixed — need a decision)

1. **Blocked online-exam state is invisible on student reads.** List keeps a blocked exam
   in `upcoming` with `studentStatus:null` (the finalized predicate is `SubmittedAt != null`),
   take-screen has no attempt-state field, `/result` returns zeros. Writes correctly 409.
   After an app restart the frontend cannot render "blocked". Minimal fix: treat `Blocked`
   like finalized in `StudentOnlineExamService.GetMyExamsAsync` (+ optionally a status field
   on take-screen/result). Contract addition — needs frontend sign-off.
2. **Raw-key controller messages** (pre-existing, shared): the JWT→TeacherStudent resolution
   helper duplicated across 6 student controllers (`StudentVideosController`,
   `StudentVideoExamsController`, `StudentOnlineExamsController`,
   `StudentAssignmentObligationsController`, `StudentAttendanceController`,
   `StudentPaymentController`) returns `{"success":false,"message":"TeacherLinkNotFound"}` —
   raw resx KEY as message, no `code` field, never localized (the keys DO exist in both resx
   files). The barcode endpoint (service-resolved) shows the correct pattern
   (`StudentNotLinkedToTeacher` / `StudentNotBoundToRoster`, localized, with codes).
3. **Self-block skips window validation**: blocking succeeds on a published exam whose
   window already ended (creates a Blocked report on an exam the student could no longer
   take). Harmless — own-report only — but arguably should 409.
4. Pre-existing, unchanged: no StudentVisibility gate on the offline-exam list (no PII);
   barcode fails-open on a null teacher config; quiz purge is replace-triggered (omit `Exam`
   on video update to KEEP the quiz); S4 `/answers` takes a SINGLE `{questionId,
   selectedOptionIds}` — the bulk wrapper belongs to `/submit` only, and the wrong wrapper
   yields a confusing 404 (CC-2-class DTO, no unmapped-member disallow).
