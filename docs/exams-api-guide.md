# Exams API — Plain-English Guide (v1.2)

A simple explanation of every `/api/exams` endpoint and what each parameter does.
Pairs with the OpenAPI file (`exams-openapi.json`).

---

## Read this first (applies to every endpoint)

- **Login token:** send `Authorization: Bearer <token>` on every request. The teacher is
  identified from the token, so you **never send a teacherId** anywhere.
- **Response shape:** every response looks like `{ success, code, message, data }`.
  - `code` — a fixed English keyword you branch on in code (e.g. `GradeExceedsMax`). It never changes with language.
  - `message` — text to show the user; English or Arabic based on the `Accept-Language` header (`en` / `ar`).
  - `data` — the actual payload.
- **Two kinds of exam (`deliveryType`):**
  - `DuringSession` — taken inside a normal class. Attendance is pulled from that class automatically and is **read-only** in the exam screens.
  - `SeparateTime` — its own date. You take attendance **inside the exam** (manual or scan).
- **`rowVersion`:** a safety token. You read it with a student and send it back when saving that
  student's grade. If someone else changed the row in the meantime, you get **409** — reload and retry.
- **Batch calls (grades & attendance) can partly fail but still return 200** — always check
  `data.allSucceeded` and each `data.items[].success` / `.code`.
- **What's new in v1.2:** create takes **sessionIds or groupIds** + one **examDate**; **home is
  paginated** and shows each exam's scope; **grades/attendance are keyed by `teacherStudentId`**
  (grades also take `examId`).

---

## 1. Create an exam — `POST /api/exams`

Creates a new exam for **either sessions or groups**, on **one date**. Groups are expanded to their
member sessions on the server. One occurrence is made per resolved session, one row per student.

**Body fields:**
- `name` *(required)* — the exam name. One name, Arabic or English. Max 200 characters.
- `notes` *(optional)* — free description.
- `deliveryType` *(required)* — `DuringSession` or `SeparateTime` (see top).
- `maxGrade` *(required)* — full mark, > 0.
- `successScore` *(required)* — passing mark, between 0 and `maxGrade`.
- `examDate` *(required)* — **one date for the whole exam.** SeparateTime: the exam's own date (today or future). DuringSession: each targeted session must have a scheduled class on that date.
- `sessionIds` *(one of these)* — recipient by sessions.
- `groupIds` *(one of these)* — recipient by groups; each expands to its member sessions. **Send EITHER `sessionIds` OR `groupIds`** (leave the other null/empty).
- `studentIds` *(optional)* — a global subset of students across the resolved sessions; omit = all.

```jsonc
// by sessions:
{ "name": "Midterm", "deliveryType": "SeparateTime", "maxGrade": 80, "successScore": 40,
  "examDate": "2026-07-20", "sessionIds": [36, 37] }
// by groups:
{ "name": "Unit Exam", "deliveryType": "SeparateTime", "maxGrade": 100, "successScore": 50,
  "examDate": "2026-07-21", "groupIds": [2] }
```

**Returns (201):** the new `examId`, and per resolved session its `occurrenceId` + assigned count.

**Common error codes:** `SelectEitherSessionsOrGroups`, `ExamDateRequired`, `SuccessScoreExceedsMax`,
`ExamRequiresMaxGrade`, `AssignmentDateInPast`, `StudentNotInSession`, `SessionHasNoStudents`;
404: `SessionNotFoundOrForeign`, `GroupNotFoundOrForeign`, `SessionOccurrenceNotFoundForDate`
(a during-session target has no class on `examDate`).

---

## 2. Pick a class date — `GET /api/exams/session-dates`

Helper for **DuringSession**: lists the class dates in a month so the teacher can see which dates
have a class (then send that date as `examDate`).

**Query parameters:** `sessionId` *(req)*, `year` *(req)*, `month` *(req, 1–12)*.
**Returns:** `{ sessionOccurrenceId, date, status }[]`. Here `status` is the **class meeting's** state
(`Pending` = attendance not started, `InProgress` = partly marked, `Completed` = all marked) — not the exam's status.

---

## 3. Exam home — `GET /api/exams/home`

The exams landing screen. Returns **paginated** `upcoming` and `past` lists (split on the teacher's
local Cairo today; upcoming ascending, past descending). One card per exam session-date.

**Query parameters:**
- `upcomingPage` *(optional, default 1)* — page of the upcoming list.
- `pastPage` *(optional, default 1)* — page of the past list.
- `pageSize` *(optional, default 20, max 100)* — rows per page (applies to both).

**Each list is** `{ data: [ ...cards ], page, pageSize, totalCount, totalPages }`.

**Each card contains:**
- `examId`, `occurrenceId` (use it in attendance calls), `name`, `deliveryType`, `sessionId`, `sessionName`, `date`.
- `assignedCount` / `totalStudents` (same total), `attendedCount`, `missedCount`, `pendingCount`, `isPast`.
- `selectionMode` — `"Sessions"` or `"Groups"` (how the exam was assigned).
- `assignedSessions` — `[{ id, name }]` (populated when assigned by sessions).
- `assignedGroups` — `[{ id, name, sessions: [{ id, name }] }]` (populated when assigned by groups, each group expanded to its sessions).

---

## 4. Open an exam — `GET /api/exams/{examId}`

One call that fills the whole opened-exam screen: header, overall stats, and each session with its
own stats and student list.

**Path parameter:** `examId` *(required)*.

**`data` contains:** `name`, `deliveryType`, `maxGrade`, `successScore`, `distinctStudentCount`
(unique heads across the exam), `globalStats`, and `sessions[]`.
- Each session: `sessionId`, `sessionName`, `occurrenceId`, `date`, `stats`, **`attendanceTaken`**
  (→ show "Edit attendance"), **`gradesTaken`** (→ show "Edit grades"), `students[]`.
- **Stats:** `totalStudents`, `gradedCount`, `average`/`highest`/`lowest` (over graded, null if none),
  `attendedCount`, `missedCount`, `pendingCount`, `belowPassingCount`.
- **Student row:** `obligationId`, `teacherStudentId`, `studentName`, `studentCode`, `status`
  (`Pending`/`Attended`/`AttendedWithGrade`/`DidNotAttend`), `attended`, `grade` (or null),
  `isGradeEntered`, `isBelowPassing`, `rowVersion`.

---

## 5. One session's list (paged) — `GET /api/exams/{examId}/sessions/{sessionId}`

Drill-in for a single session inside an exam, with paging and search.
`page` *(default 1)*, `pageSize` *(default 50, max 200)*, `search` *(name or code)*. Same student rows as #4.

---

## 6. Save grades — `PUT /api/exams/grades`

Enter, change, or clear grades for many students at once, **keyed by student**.

**Body:**
- `examId` *(required)* — the exam these grades belong to.
- `items[]` — one per student:
  - `teacherStudentId` *(required)* — the student (resolved to their single row in the exam).
  - `grade` — the mark, 0 ≤ grade ≤ maxGrade. **`null` = clear** the grade (student stays `Attended`).
  - `rowVersion` *(required)* — the token you last read for that student.

```jsonc
{ "examId": 12, "items": [ { "teacherStudentId": 761, "grade": 70, "rowVersion": "AAAAAAAAB9E=" } ] }
```

**Rules:** entering a grade → `AttendedWithGrade`; can't grade an absent student (`CannotGradeAbsentStudent`);
during-session unmarked student can't be graded (`AttendanceNotRecordedForExam`); clearing skips these.

**Returns (200):** `updatedCount`, `allSucceeded`, and `items[]` each with `teacherStudentId`,
`obligationId`, `success`, `code`, new `status`, new `grade`, fresh `rowVersion`.
Item codes: `GradeExceedsMax`, `GradeOutOfRange`, `CannotGradeAbsentStudent`, `AttendanceNotRecordedForExam`,
`ObligationNotFound` (student not in the exam), `AmbiguousStudentInExam`. Whole-call: `ObligationConcurrencyConflict` (409),
`ExamNotFound` (404).

---

## 7. Take / edit attendance — `PUT /api/exams/attendance` (separate-time only)

Mark students present or absent for a **SeparateTime** exam, **keyed by student**.

**Body:**
- `occurrenceId` *(required)* — the exam's session instance (from the exam view or home card).
- `items[]` — one per student: `teacherStudentId` *(required)*, `present` *(required, true/false)*.

**Rules:** `present:true` → `Attended` (keeps a grade); `present:false` → `DidNotAttend` **and deletes the grade**.
During-session → 409 `AttendanceReadOnlyForDuringSession`.

**Returns (200):** `items[]` with `teacherStudentId`, `obligationId`, new `status`, fresh `rowVersion`.

---

## 8. Scan attendance — `POST /api/exams/attendance/scan` (separate-time only)

Scan a student's QR/barcode (their student code) to mark them present. `occurrenceId` *(req)*, `code` *(req)*.
Idempotent — returns `alreadyProcessed`, keeps any grade.

---

## 9. Home dashboard (exam-aware) — `GET /api/attendance/dashboard`

The normal app home/attendance dashboard, now exam-aware. `date` *(optional)*. Session cards carry
`isExamSession` (+ `examId`/`examOccurrenceId`/`examName`); `examsToday[]` lists separate-time exams due today.

---

## `obligationId` vs `teacherStudentId`

`obligationId` is a student's slot in one exam session-date. As of v1.2, the **write** endpoints take
**`teacherStudentId`** (each student has one row per exam), while every read row still returns
`obligationId` too — so you can use whichever you have.

## The typical flow

1. **Create** → `POST /api/exams` (sessions or groups + one date; use `GET /session-dates` to find during-session dates).
2. **Exam home** → `GET /api/exams/home` (paginated upcoming/past, with scope on each card).
3. **Open exam** → `GET /api/exams/{examId}` (`attendanceTaken`/`gradesTaken` decide Take vs Edit).
4. **Separate-time:** attendance → `PUT /api/exams/attendance` or scan → `POST /api/exams/attendance/scan`. **During-session:** attendance comes from the class (read-only).
5. **Grades** → `PUT /api/exams/grades` (by `examId` + `teacherStudentId`; `grade:null` clears).
