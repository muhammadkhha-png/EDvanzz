# Frontend ↔ Backend Gap Audit

Differences between the **mobile app** (Flutter) and the **API** (.NET backend).
Every module **except** parent, homework, messaging, notifications, and permissions.
One module at a time. Each issue has three parts: **Issue**, **API related to**, **What to fix**.

---

## Module 1 — Auth (login, sign up, OTP, passwords, Google sign-in)

Login, sign up, OTP, refresh, logout, change password, complete profile, and Google sign-in
all match between the app and the API. The items below are the exceptions.

### A-1 — Forgot Password cannot reset the password 🔴
- **Issue:** The app has a Forgot Password screen. The user types their phone, gets an OTP, and
  enters it — then the app just shows "password reset not available" and returns to login. A
  user who forgets their password has no way back in, because the backend has no reset endpoint.
- **API related to:** `POST api/auth/reset-password` and `POST api/auth/forgot-password` — the
  app has these addresses ready but **they don't exist on the backend**.
- **What to fix:** Backend builds a reset-password endpoint (used after the OTP is verified).
  Then frontend connects the new-password screen to it and removes the "not available" message.

### A-2 — OTP code is returned in the response and shown on screen 🟢 (known, leave for now)
- **Issue:** When the app asks for an OTP, the backend sends the actual code back in the
  response and the app shows it on screen. Normally the code should arrive by SMS and never be
  shown. This is on purpose for now because there is no SMS service connected yet.
- **API related to:** `POST api/auth/generate-otp` (returns the code in `data`).
- **What to fix:** Nothing now. Once SMS is connected: backend stops returning the code, and
  frontend removes the on-screen code. Noted so it isn't forgotten.

### A-3 — Teachers need an extra call after login to get their own ID 🟡
- **Issue:** Most teacher screens need the teacher's own ID in the URL. Login returns this ID
  for students and assistants but **not for teachers**, so the app makes a second call right
  after login just to learn the teacher's ID. This makes login slower.
- **API related to:** `POST api/auth/login` (missing the teacher's ID in the response) and the
  extra call `GET api/Teacher/{id}/profile`.
- **What to fix:** Backend adds the teacher's ID to the login response. Tell the frontend team
  so they can drop the extra call once it's there.

### A-4 — "signup" is misspelled in the Google address 🟢 (cosmetic)
- **Issue:** The Google sign-in address is spelled `sigup-by-google` (missing an "n"). It works
  because both sides match — just ugly.
- **API related to:** `POST api/auth/sigup-by-google`.
- **What to fix:** Rename to `signup-by-google` on the backend and the app together, one day.

---

## Module 2 — Students (student list: add, edit, delete, recycle bin, import, barcodes)

Adding, editing, deleting, the list, filters, sorting, the recycle bin, bulk import, and
barcodes all match. Three items below.

### B-1 — The "change session" dropdown never appears on the Edit Student screen 🔴
- **Issue:** When editing a student, there should be a dropdown to move them to another
  session. The app builds that dropdown from a list of the teacher's sessions it expects to
  come back with the student's details, but the backend only returns the single session the
  student is already in. With no list, the app hides the dropdown. A teacher can never change a
  student's session from this screen.
- **API related to:** `GET api/teacherstudent/students/{id}` — its response has no list of the
  teacher's sessions (only the one assigned session).
- **What to fix:** Backend adds the teacher's session list to that response (the shape the app
  already expects), **or** frontend loads the session list from the sessions endpoint instead.

### B-2 — The student list loads with two calls when one would do 🟡
- **Issue:** The app makes two calls every time the list loads — one for the list and one for
  the counts — even though the backend has a single endpoint that returns both together. There
  is also leftover app code for that unused endpoint that expects a data shape the backend
  doesn't send.
- **API related to:** `GET api/teacherstudent/students/overview` (unused single call) vs.
  `GET api/teacherstudent/students` + `GET api/teacherstudent/counts` (the two calls used now).
- **What to fix:** Frontend switches to the single `students/overview` call, or deletes the
  unused leftover code. Tell the frontend team.

### B-3 — Bulk import shows only a success count, not the imported students 🟢 (minor)
- **Issue:** After a bulk import, the backend returns the full list of students that succeeded
  (names and codes) plus the rows that failed. The app shows failed rows in detail but only a
  number for the successes — it ignores the success list.
- **API related to:** `POST api/teacherstudent/bulk-import` (response field `succeeded[]` is
  ignored by the app).
- **What to fix:** Frontend can display the imported students in detail if wanted. Optional.

---

## Module 3 — Sessions & Groups (create/edit sessions, groups, links, assign students)

This module is very well aligned. Every endpoint the app calls exists on the backend, the
request and response field names match, the sort options match, and the day/occurrence/payment
values match. One real problem below, plus one small note.

### C-1 — The group "description" the app forces you to type is thrown away 🟡 ✅ FIXED (backend)
- **Issue:** On the "Create Group" screen the app makes the user type a **description** and
  won't let them submit without one. The app then sends that description to the backend — but
  the backend had no place for it. It was silently ignored, never saved, and never shown again.
  So the user was forced to fill in a field that did nothing.
- **API related to:** `POST api/session/groups`, `PUT api/session/{teacherId}/groups/{groupId}`,
  `GET api/session/{teacherId}/groups`.
- **What was done (backend):** Added a `Description` field to session groups end to end:
  - `SessionGroup` entity gets a nullable `Description` column (migration
    `20260722165401_AddSessionGroupDescription`, additive `nvarchar(1000)` NULL).
  - `CreateSessionGroupDto` now accepts `description`; it is saved on create.
  - `SessionGroupDto` now **returns** `description` on create, list, and rename responses.
  - `RenameSessionGroupDto` also accepts an optional `description`: `null` leaves the existing
    one unchanged, an empty string clears it, any other value updates it.
  - The description the app already sends is now stored and echoed back — no frontend change
    is required, and the forced-input now leads somewhere.
- **Status:** Implemented and builds clean. Ships with the next deploy (migration runs in CI).

### C-2 — "Ungrouped only" filter exists on the backend but the app doesn't use it 🟢 (minor)
- **Issue:** The backend list endpoint can filter to only sessions that are not in any group
  (by passing `groupId = -1`). The app never sends this, so that filter option isn't available
  to the user.
- **API related to:** `GET api/session/{teacherId}/sessions?groupId=-1`.
- **What to fix:** Frontend can add the "ungrouped only" filter using `groupId = -1` if the
  design calls for it. Optional.
