# Edvanz Files API - Frontend Guide

Base URL: `https://app-edvanz-api-prod.azurewebsites.net/api`
Date: 16/07/2026 (v2 - after the "video photo" rename)

How file handling works in short: you upload the file first on its own endpoint, you get back a
`fileId` and a `url`. After that you send the `fileId` inside the create/update request of the
video or the exam, and you use the `url` any time you want to display the file. The `url` is
protected, it needs the JWT token on every request and the server checks on every call if this
user is allowed to see this file or not. There are no public file links.

All requests below need the header `Authorization: Bearer <token>` unless written otherwise.
All responses come inside the standard envelope:

```json
{ "success": true, "code": "Success", "message": "Done successfully", "data": ... }
```

Upload categories (the `category` field decides who can see the file later):

| category | used for | who can see it |
|----------|----------|----------------|
| VideoPhoto | the video cover image | teacher, his assistants, students the video is scoped to |
| VideoAttachment | PDF attached to a video | teacher, his assistants, students the video is scoped to |
| OnlineExamQuestionImage | image on an online exam question | teacher, his assistants, students assigned to the exam |
| VideoExamQuestionImage | image on a video exam question | teacher, his assistants, students the video is scoped to |

Students only see video files when the video is Published and its publish date has passed.
When the teacher deletes the video or the exam, the students lose access immediately.

Limits: images (jpeg, png, gif, webp, bmp, tiff, heic) and pdf only. Max 10 MB per file, max 10
files per request, max 50 MB per request total.

---

## Scenario 1: Video with photo and PDF attachment (create until the student sees it)

### Step 1 - Upload the video photo

**Endpoint:** `POST /upload` (multipart/form-data)

**Payload:**

```
files    = cover.jpg            (the image file)
category = VideoPhoto
```

**Response (201):**

```json
{
  "success": true,
  "code": "Success",
  "message": "Done successfully",
  "data": [
    {
      "fileId": "4326581e-3f9d-480c-82ed-050cbc5cf84e",
      "url": "https://app-edvanz-api-prod.azurewebsites.net/api/files/4326581e-3f9d-480c-82ed-050cbc5cf84e",
      "originalName": "cover.jpg",
      "size": 204800,
      "mimeType": "image/jpeg"
    }
  ]
}
```

Keep the `fileId`, you will send it in step 3. You can upload more than one file in the same
request (repeat the `files` field), you get one item per file in `data`.

### Step 2 - Upload the PDF attachments

You can upload several in one request (repeat the `files` field) - each gets its own fileId.

**Endpoint:** `POST /upload` (multipart/form-data)

**Payload:**

```
files    = lesson-notes.pdf
files    = homework.pdf
category = VideoAttachment
```

**Response (201):** same shape as step 1, take the `fileId`.

### Step 3 - Create the video

**Endpoint:** `POST /videos` (application/json - no more multipart here)

**Payload:**

```json
{
  "title": "Lesson 1",
  "description": "Intro lesson",
  "sourceUrl": "https://youtu.be/dQw4w9WgXcQ",
  "videoPhotoFileId": "4326581e-3f9d-480c-82ed-050cbc5cf84e",
  "attachmentFileIds": ["bd3af1f1-c8cb-45ba-96f9-7e62202467bc", "77aa1122-3344-5566-7788-99aabbccddee"],
  "scopes": [ { "scopeType": "Session", "ids": [38] } ],
  "exam": {
    "title": "Quick quiz",
    "questions": [
      {
        "text": "Which shape is in the picture?",
        "questionType": "SingleChoice",
        "imageFileId": "a5e6895d-db93-4c45-b9cf-5d8d8f1bff17",
        "options": [
          { "text": "Square", "isCorrect": true },
          { "text": "Circle", "isCorrect": false }
        ]
      }
    ]
  }
}
```

`videoPhotoFileId`, `attachmentFileIds` and the whole `exam` block are optional. A video can
hold up to 10 attachments (422 VideoAttachmentsLimitExceeded above that). The question
`imageFileId` must be uploaded with category `VideoExamQuestionImage` (step like 1 but with that
category).

**Response (201):**

```json
{
  "success": true,
  "code": "VideoCreated",
  "message": "Video added successfully",
  "data": {
    "videoAssetId": 6,
    "videoPhotoReadUrl": "https://.../api/files/4326581e-3f9d-480c-82ed-050cbc5cf84e",
    "attachments": [
      {
        "id": "bd3af1f1-c8cb-45ba-96f9-7e62202467bc",
        "fileName": "lesson-notes.pdf",
        "contentType": "application/pdf",
        "fileSizeBytes": 189,
        "readUrl": "https://.../api/files/bd3af1f1-c8cb-45ba-96f9-7e62202467bc"
      }
    ],
    "scopesAdded": 1,
    "studentsInScope": 1,
    "examId": 4
  }
}
```

Errors you can get on this step: 404 FileNotFound (wrong fileId), 403 FileNotOwned (fileId of
another account), 400 FileCategoryMismatch (for example an OnlineExamQuestionImage id in the
videoPhotoFileId field), 409 FileAlreadyInUse (the file is already attached to another video).

### Step 4 - The student opens his videos list

**Endpoint:** `GET /videos/student/teachers/{teacherId}?page=1&pageSize=20` (student token)

**Payload:** none.

**Response (200):**

```json
{
  "success": true,
  "data": {
    "data": [
      {
        "id": 6,
        "title": "Lesson 1",
        "description": "Intro lesson",
        "sourceType": "YouTube",
        "sourceUrl": "https://youtu.be/dQw4w9WgXcQ",
        "durationSeconds": 0,
        "assignedAt": "2026-07-16T15:10:00Z",
        "hasOpened": false,
        "lastOpenedAt": null,
        "videoPhotoUrl": "https://.../api/files/4326581e-3f9d-480c-82ed-050cbc5cf84e",
        "attachments": [
          {
            "id": "bd3af1f1-c8cb-45ba-96f9-7e62202467bc",
            "fileName": "lesson-notes.pdf",
            "contentType": "application/pdf",
            "fileSizeBytes": 189,
            "readUrl": "https://.../api/files/bd3af1f1-c8cb-45ba-96f9-7e62202467bc"
          }
        ]
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 1,
    "totalPages": 1
  }
}
```

`videoPhotoUrl` is for the card cover (null when none); `attachments` is the list of handouts
(empty when none) - show a download row per item.

### Step 5 - The student displays the photo / opens the PDF

**Endpoint:** `GET /files/{fileId}` (this is exactly the `videoPhotoUrl` / `readUrl` value)

**Payload:** none, just the Authorization header.

**Response:** `302 Found` with a `Location` header pointing to a temporary storage link
(valid around 4 hours). The client must follow the redirect. Without a token it returns 401,
and a student that is not in the video scope gets 403.

In Flutter this is one line, the http client follows redirects by itself:

```dart
Image.network(videoPhotoUrl, headers: {'Authorization': 'Bearer $token'});
```

For web, `<img>` cannot send headers, so fetch it with the token and turn it into a blob url:

```js
const res = await fetch(url, { headers: { Authorization: `Bearer ${token}` } });
imgEl.src = URL.createObjectURL(await res.blob());
```

---

## Scenario 2: Updating the video files

### Step 0 - Load the edit screen (this is where you get the current fileIds)

**Endpoint:** `GET /videos/{videoAssetId}`

**Response (200):** the video detail. The parts that matter for editing files:

```json
{
  "data": {
    "id": 6,
    "title": "Lesson 1",
    "rowVersion": "AAAAAAAAF3k=",
    "videoPhotoFileId": "4326581e-3f9d-480c-82ed-050cbc5cf84e",
    "videoPhotoUrl": "https://.../api/files/4326581e-3f9d-480c-82ed-050cbc5cf84e",
    "attachments": [
      { "id": "bd3af1f1-c8cb-45ba-96f9-7e62202467bc",
        "fileName": "lesson-notes.pdf",
        "readUrl": "https://.../api/files/bd3af1f1-c8cb-45ba-96f9-7e62202467bc" }
    ],
    "exam": {
      "questions": [
        { "text": "Q1?", "imageFileId": "a5e6895d-...", "imageUrl": "https://.../api/files/a5e6895d-..." }
      ]
    }
  }
}
```

So: `videoPhotoFileId` is the current photo id, every item in `attachments` carries its own
`id`, and each exam question carries its `imageFileId`. Display with the urls, resend the ids.

### Step 1 - Change the video photo

First upload the new image like scenario 1 step 1 (category VideoPhoto), then:

**Endpoint:** `PUT /videos/{videoAssetId}/video-photo` (application/json)

**Payload:**

```json
{ "videoPhotoFileId": "4b287518-cc16-47d2-b929-19d77e21eafc" }
```

**Response (200):**

```json
{
  "success": true,
  "code": "VideoPhotoReplaced",
  "message": "Video photo replaced successfully",
  "data": { "readUrl": "https://.../api/files/4b287518-cc16-47d2-b929-19d77e21eafc" }
}
```

The old photo is released automatically and gets cleaned from storage by a background job. The
old url stops working for students immediately. Sending the current photo id again is a no-op,
it just returns 200 with the same url.

### Step 2 - Change the attachments (replace-all list)

Attachments are changed through the normal video update. `attachmentFileIds` works like a
replace-all set, same idea as `scopes`:

- not sent (null) = leave the attachments as they are
- `[]` = remove all attachments
- `["id1", "id2"]` = this becomes the exact new set: ids that are already on the video stay,
  ids you removed from the list are released, new ids get attached. Max 10.

Note: `PUT /videos/{id}` is a full update, you send the whole video body again with the
`rowVersion` you got from `GET /videos/{id}` (send it back exactly as you received it).

**Endpoint:** `PUT /videos/{videoAssetId}` (application/json)

**Payload (keep one existing + add one new):**

```json
{
  "title": "Lesson 1",
  "sourceUrl": "https://youtu.be/dQw4w9WgXcQ",
  "rowVersion": "AAAAAAAAF3k=",
  "attachmentFileIds": [
    "bd3af1f1-c8cb-45ba-96f9-7e62202467bc",
    "e0c11111-2222-3344-5566-77889900aabb"
  ]
}
```

**Payload (remove all):**

```json
{
  "title": "Lesson 1",
  "sourceUrl": "https://youtu.be/dQw4w9WgXcQ",
  "rowVersion": "AAAAAAAAF3k=",
  "attachmentFileIds": []
}
```

(`"removeAttachment": true` with no list does the same as the empty list.)

**Response (200):** the full video detail with the final `attachments` list.
A 409 here means somebody else edited the video in the meantime, re-GET and retry.

### Step 3 - Delete the video

**Endpoint:** `DELETE /videos/{videoAssetId}`

**Response (200):** `{ "success": true, "code": "VideoDeleted", ... }`

All the files of the video (photo, attachment, exam question images) are released and the
students lose access to them at the same moment.

---

## Scenario 3: Online exam with question images (create until the student sees it)

### Step 1 - Upload the question image

**Endpoint:** `POST /upload` (multipart/form-data)

**Payload:**

```
files    = question1.png
category = OnlineExamQuestionImage
```

**Response (201):** same shape as always, keep the `fileId`.

### Step 2 - Create the exam

**Endpoint:** `POST /online-exams` (application/json)

**Payload:**

```json
{
  "title": "Algebra quiz",
  "description": "Chapter 3",
  "instructions": "45 minutes, no calculator",
  "startDateTime": "2026-07-20T10:00:00Z",
  "endDateTime": "2026-07-20T12:00:00Z",
  "passPercentage": 50,
  "visibility": true,
  "scopes": [ { "scopeType": "Session", "sessionId": 38 } ],
  "questions": [
    {
      "questionText": "Which shape is in the image?",
      "questionType": "SingleChoice",
      "degree": 5,
      "imageFileId": "d14f2bfe-b004-4166-b6ff-a06bbad6a5b3",
      "options": [
        { "optionText": "Square", "isCorrect": true },
        { "optionText": "Circle", "isCorrect": false }
      ]
    }
  ]
}
```

`imageFileId` is optional per question. Note the field names here are `questionText` /
`optionText` (the video exam uses `text`, they are two different modules).

**Response (201):** the exam detail with `"status": "Draft"`, its `id` and `rowVersion`.

### Step 3 - Publish the exam

The exam is created as Draft, students see nothing until you publish it.

**Endpoint:** `PATCH /online-exams/{onlineExamId}/status`

**Payload:**

```json
{ "status": "Published", "rowVersion": "AAAAAAAAF6M=" }
```

`rowVersion` comes from the create response or from `GET /online-exams/{id}`.

**Response (200):**

```json
{
  "success": true,
  "code": "OnlineExam.Published",
  "data": { "status": "Published", "rowVersion": "AAAAAAAAF6Q=" }
}
```

IMPORTANT - the rowVersion rule: every successful status change gives the exam a NEW
rowVersion, and the response returns it in `data.rowVersion`. For the NEXT status change
(for example Published back to Draft) you must send this new value, not the old one. Sending
an old rowVersion is what produces the 409 "changed by someone else - please refresh" error.
So: keep always the rowVersion from the LAST response (or from a fresh GET), never reuse an
old one.

### Step 4 - The student opens the exam list

**Endpoint:** `GET /online-exams/student/teachers/{teacherId}` (student token)

**Response (200):** the exams available for this student with their status and dates.

### Step 5 - The student opens the take screen

**Endpoint:** `GET /online-exams/student/teachers/{teacherId}/{onlineExamId}/questions`

**Response (200):**

```json
{
  "success": true,
  "data": {
    "examId": 20,
    "examName": "Algebra quiz",
    "instructions": "45 minutes, no calculator",
    "startDateTime": "2026-07-20T10:00:00Z",
    "endDateTime": "2026-07-20T12:00:00Z",
    "examDegree": 5,
    "questions": [
      {
        "id": 55,
        "questionText": "Which shape is in the image?",
        "questionType": "SingleChoice",
        "degree": 5,
        "sortOrder": 0,
        "imageUrl": "https://.../api/files/d14f2bfe-b004-4166-b6ff-a06bbad6a5b3",
        "options": [
          { "id": 90, "optionText": "Square", "sortOrder": 0 },
          { "id": 91, "optionText": "Circle", "sortOrder": 1 }
        ]
      }
    ]
  }
}
```

`imageUrl` is null when the question has no image. Correct answers are never in this response.

### Step 6 - The student displays the image

Same as scenario 1 step 5: `GET` the `imageUrl` with the student token, follow the 302. A
student that is not assigned to this exam gets 403 on the same url.

---

## Scenario 4: Updating exam questions and their images

Questions can only be edited while the exam is Draft (unpublish first with the status endpoint
if needed: `{ "status": "Draft", "rowVersion": ... }` - allowed only when nobody submitted yet).

**Endpoint:** `PUT /online-exams/{onlineExamId}/questions` (replace all questions)

**Payload:** the same `questions` array shape as the create.

- To KEEP an existing image: send its fileId again in `imageFileId`. You get it from
  `GET /online-exams/{id}/questions` - every question comes back with its `imageFileId` (and
  `imageUrl` for display). As a fallback, the fileId is also the last part of any file url
  after `/api/files/`.
- To CHANGE an image: upload a new file and send the new fileId.
- To REMOVE an image: send `"imageFileId": null` (or leave it out).

**Response (200):** `{ "success": true, "code": "OnlineExam.Updated", ... }`

The images that were removed or replaced are released and cleaned automatically. Deleting the
exam (`DELETE /online-exams/{id}`) releases all its images the same way.

---

## Scenario 5: Managing an uploaded file before attaching it

If the user uploaded a wrong file and wants to fix it before saving the video/exam:

### Replace it

**Endpoint:** `PUT /upload` (multipart/form-data)

**Payload:**

```
fileId = 4326581e-3f9d-480c-82ed-050cbc5cf84e     (the old one)
file   = new-cover.jpg                             (single file)
```

**Response (200):** one descriptor like the upload response but a single object, with a NEW
`fileId` and `url`. Important: store the new id, the old one is gone. Only the account that
uploaded the file can replace it, others get 403 FileNotOwned.

### Delete it

**Endpoint:** `DELETE /upload?fileId=4326581e-3f9d-480c-82ed-050cbc5cf84e`

**Response (200):** `{ "success": true, "data": true }`

Also good to know: files that are uploaded but never attached to anything are cleaned
automatically after 24 hours, so an abandoned upload is not a problem.

---

## Scenario 6: Sign-up with the national ID image

Nothing changed here, sign-up is still multipart with the image inside the same request:

**Endpoint:** `POST /auth/sign-up` (multipart/form-data, no token)

**Payload:**

```
userType          = Student
fullName          = Ahmed Mohamed
username          = ahmed_m
password          = ...
confirmedPassword = ...
phoneNumber       = 01xxxxxxxxx
idImage           = id-photo.jpg        (optional)
```

**Response (200):** `{ "success": true, "code": "SuccessSaving", ... }`

The ID image is stored privately and only the account owner and the admins can ever open it.

---

## Errors reference

| HTTP | code | when |
|------|------|------|
| 400 | FileInvalidCategory / FileCategoryNotUploadable | category missing, unknown, or not allowed on /upload |
| 400 | FileCategoryMismatch | the fileId is from another category than the field expects |
| 401 | - | missing or expired token on any file url |
| 403 | FileNotOwned | not your file / the resource does not give you access |
| 404 | FileNotFound | unknown fileId |
| 409 | FileAlreadyInUse | the file is already attached to another item |
| 422 | UploadInvalidType / UploadFileTooLarge / UploadNoFiles / UploadTooManyFiles | upload validation |

All the error messages come localized (Accept-Language: en / ar).

## Where to get the fileId when you update something

| file | read it from | field |
|------|--------------|-------|
| video photo | GET /videos/{id} | `videoPhotoFileId` |
| video attachments | GET /videos/{id} | `attachments[].id` |
| video exam question image | GET /videos/{id} | `exam.questions[].imageFileId` |
| online exam question image | GET /online-exams/{id}/questions | `data[].imageFileId` |
| fresh upload | POST /upload response | `data[].fileId` |

And in the worst case, every gated url ends with the fileId, so `url.split('/').last` always
works.

## General notes

1. One uploaded file can be attached to exactly one item. If you need the same picture on two
   questions, upload it twice.
2. Never cache the redirect target (the storage link) for more than a couple of hours. Cache the
   `/api/files/{fileId}` url itself as much as you want, it is permanent while the file exists.
3. The old endpoints that took the files as multipart inside create video, and the route
   `PUT /videos/{id}/thumbnail`, do not exist anymore. Everything file related goes through
   `/upload` first, and the photo route is `/videos/{id}/video-photo` now.
