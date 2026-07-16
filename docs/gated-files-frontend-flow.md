# Gated Files — Frontend Integration Flow

**Base URL:** `https://app-edvanz-api-prod.azurewebsites.net/api`
All responses use the standard envelope `{ success, code, message, data }`.

## The model in one paragraph

Files are uploaded **once** to `POST /upload` and referenced everywhere else by **`fileId`**
(a GUID). Every file also gets a **stable gated URL** `…/api/files/{fileId}` for display.
That URL requires a **valid JWT** and re-checks access on **every** fetch (owner teacher ✓,
their assistants ✓, students **only** when the resource — e.g. the exam — grants it ✓,
everyone else 403, no token 401). On success it answers **302 → a short-lived (~4 h) Azure
link** that serves the bytes. There are **no public file URLs** — a raw blob link without a
token is always rejected.

### Golden rules

1. `fileId` = for **attaching** to resources. `url` = for **displaying**. The `fileId` is
   also the last path segment of the `url` — you can always recover one from the other.
2. Every image/PDF request must send `Authorization: Bearer <jwt>` **and follow redirects**.
3. Upload requires a **`category`** — it must match the slot you'll attach to:
   | category | attach into | students see it? |
   |---|---|---|
   | `VideoPhoto` (the video's cover image) | `videoPhotoFileId` (create/update video, `PUT /videos/{id}/video-photo`) | ✅ students the video is scoped to |
   | `VideoAttachment` | `attachmentFileId` (create/update video) | ✅ students the video is scoped to |
   | `OnlineExamQuestionImage` | `questions[].imageFileId` (online exams) | ✅ students assigned to the exam |
   | `VideoExamQuestionImage` | `exam.questions[].imageFileId` (video exams) | ✅ students the video is scoped to |

   Student access always requires the video to be **Published** (and its publish date reached) /
   the exam to be assigned — access dies instantly when the resource is deleted or unscoped.
4. One upload attaches to **one** resource (re-attaching elsewhere → `409 FileAlreadyInUse`).
5. Don't cache the 302 target past ~4 h — re-request the gated `url` instead; it never expires.

---

## Flow A — Teacher creates an online exam with an image on a question

**Step 1 — upload the image** (as the logged-in teacher):

```
POST /upload
Content-Type: multipart/form-data
  files    = <question.png>            (repeat the field for multiple files)
  category = OnlineExamQuestionImage
```
```json
201 → { "data": [ {
  "fileId": "d14f2bfe-b004-4166-b6ff-a06bbad6a5b3",
  "url": "https://…/api/files/d14f2bfe-b004-4166-b6ff-a06bbad6a5b3",
  "originalName": "question.png", "size": 70, "mimeType": "image/png"
} ] }
```

**Step 2 — create the exam, sending the `fileId`:**

```json
POST /online-exams
{
  "title": "Algebra quiz",
  "startDateTime": "2026-07-20T10:00:00Z",
  "endDateTime":   "2026-07-20T12:00:00Z",
  "passPercentage": 50,
  "visibility": true,
  "scopes": [ { "scopeType": "Session", "sessionId": 38 } ],
  "questions": [ {
    "questionText": "Which shape is in the image?",
    "questionType": "SingleChoice",
    "degree": 5,
    "imageFileId": "d14f2bfe-b004-4166-b6ff-a06bbad6a5b3",
    "options": [ { "optionText": "Square", "isCorrect": true },
                 { "optionText": "Circle", "isCorrect": false } ]
  } ]
}
```
`201` — the file is now attached to that question. Reading the questions back
(`GET /online-exams/{id}/questions`) returns each row with **`imageUrl`** (the gated URL).

## Flow B — Student sees the image

The take screen (`GET` student online-exam take endpoint) returns each question with its
`imageUrl`. Render it **with the JWT**:

```dart
Image.network(
  question.imageUrl,                                   // follows the 302 automatically
  headers: { 'Authorization': 'Bearer $jwt' },
)
```

- Student **assigned** to the exam → image loads (302 → bytes). Verified live.
- Anyone else → **403**; no token → **401**. If the exam is deleted, assigned students lose
  access **immediately**.
- Web (`<img>` can't send headers): `fetch(url, {headers})` → `blob:` object URL, or open the
  302's Location directly (valid ~4 h).

## Flow C — Editing

- **Change a question's image:** upload a new file (Step 1) → send the new `imageFileId` in
  the replace-questions request. Old image is released and cleaned up automatically.
- **Keep an existing image** when re-submitting questions: send its current `fileId` — if you
  only have `imageUrl`, the `fileId` is its **last path segment**.
- **Remove an image:** send `imageFileId: null`.
- **Standalone replace/delete of an un-attached upload:**
  `PUT /upload` (multipart `fileId` + `file`) → returns a **new** `fileId` (store it — the
  old one is gone); `DELETE /upload?fileId={guid}` → 200. Only the uploader (or SuperAdmin)
  may do either — anyone else gets `403 FileNotOwned`.

## Flow D — Video with a photo (cover image) + PDF (plain JSON — no more multipart!)

```
POST /upload  (files=<cover.jpg>,  category=VideoPhoto)       → fileId T
POST /upload  (files=<notes.pdf>,  category=VideoAttachment)  → fileId A
```
```json
POST /videos
{
  "title": "Lesson 1",
  "sourceUrl": "https://youtu.be/…",
  "videoPhotoFileId": "T",
  "attachmentFileId": "A",
  "scopes": [ { "scopeType": "Session", "ids": [38] } ]
}
```
Response carries `videoPhotoReadUrl` + `attachment.readUrl` (gated URLs). Update video /
`PUT /videos/{id}/video-photo` `{ "videoPhotoFileId": "…" }` work the same way; the old
attachment-download endpoint is gone — use `attachment.readUrl` directly.

**Students the video is scoped to see these files too.** The student video list
(`GET /videos/student/teachers/{teacherId}`) now returns per row:
```json
{ "id": 12, "title": "Lesson 1", "sourceUrl": "…",
  "videoPhotoUrl": "https://…/api/files/{fileId}",          // cover for the card, or null
  "attachment": { "fileName": "notes.pdf", "readUrl": "…" } // handout, or null
}
```
Render/download them with the student's JWT exactly like exam images (Flow B). Draft or
future-scheduled videos never expose their files to students.

## Errors you may see

| HTTP | `code` | Meaning |
|---|---|---|
| 400 | `FileInvalidCategory` / `FileCategoryNotUploadable` | `category` missing/unknown, or not allowed via `/upload` (e.g. `NationalIdImage`) |
| 400 | `FileCategoryMismatch` | fileId's category doesn't fit the slot (thumbnail file into an exam image, …) |
| 401 | — | no/expired JWT on the gated URL |
| 403 | `FileNotOwned` | not yours / resource doesn't grant you this file |
| 404 | `FileNotFound` | unknown fileId |
| 409 | `FileAlreadyInUse` | file already attached to another resource |
| 422 | `UploadInvalidType` / `UploadFileTooLarge` / `UploadNoFiles` / `UploadTooManyFiles` | images+PDF only, ≤10 MB/file, 1–10 files/request |

Sign-up is unchanged (multipart `idImage` as before) — the national-ID image is handled
server-side and visible only to its owner and admins.
