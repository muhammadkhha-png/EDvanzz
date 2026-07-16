# Gated file registry — remaining steps before merge/deploy

This branch migrates all file handling to the central `FileObject` registry served through the
gated `GET /api/files/{fileId}` endpoint (private blob + resource-scoped authorization). All
application/domain/infrastructure/API code is in place. Two steps could **not** be performed in the
automated environment and must be done on a machine with the .NET 10 SDK (the SDK download —
`builds.dotnet.microsoft.com` — is blocked by this session's egress policy, so `dotnet build` and
`dotnet ef` could not run here). **The code has not been compiled locally; expect to fix minor
compile issues surfaced by the first build.**

## 1. Generate the EF migration (required — CI's model-coverage gate depends on it)

Model changes made (see `EdvanzDbContext.OnModelCreating`):
- **New table `FileObjects`** — entity `Edvanz.Domain.Entities.FileObject` (unique index on
  `PublicId`; index on `Status`; NoAction FK `VideoAssetId` → `VideoAssets`).
- **`VideoAssets`**: drop `ThumbnailBlobPath`; add `ThumbnailFileId` (long?, NoAction FK → FileObjects).
- **`VideoExamQuestions`**: add `ImageFileId` (long?, NoAction FK → FileObjects).
- **`OnlineExamQuestions`**: add `ImageFileId` (long?, NoAction FK → FileObjects).
- **`Users`**: drop `IdImage` (varbinary); add `IdImageFileId` (long?, NoAction FK → FileObjects).
- **Drop table `VideoAttachments`** (folded into `FileObjects`, category `VideoAttachment`).

Run from the repo root:

```bash
dotnet tool restore
dotnet ef migrations add FileObjectRegistry --project Edvanz.Infrastructure --startup-project Edvanz.API
dotnet ef migrations has-pending-model-changes --project Edvanz.Infrastructure --startup-project Edvanz.API  # must report none
dotnet build Edvanz.slnx
```

Verify a fresh-DB apply (BUG-9 precedent) before pushing to `master_integration`.

**Data note (confirmed acceptable):** the migration drops existing `User.IdImage` bytes and the
`VideoAttachments` table; old `video-attachments`-container blobs are abandoned. Confirm prod data on
these surfaces is negligible first.

## 2. Azure infra — make the uploads container private (out-of-band, like the original upload rollout)

```bash
az storage account update -n <storage-account> -g <resource-group> --allow-blob-public-access false
az storage container set-permission -n uploads --public-access off
```

`appsettings.json` already renamed `AzureBlobStorage:PublicContainerName` → `UploadsContainerName`
(value still `"uploads"`) and added `UploadsSasLifetimeMinutes` (default 240) and
`UploadsPendingGraceHours` (default 24, used by the `file-object-gc` Hangfire job).

## Verification (after build passes)

See the plan's verification section: anonymous blob URL → 403; upload → gated URL → 302 with JWT,
401 anonymous; online-exam image visible to an assigned student, 403 to others; delete/replace →
FileObject `Detached` → reaped by the hourly `file-object-gc` job; sign-up idImage → owner/admin only.

## Pre-existing, out of scope

`IStudentOnlineExamService` has no DI registration in `Program.cs` (the controller injecting it would
fail to resolve). This predates and is unrelated to this change; add
`builder.Services.AddScoped<IStudentOnlineExamService, StudentOnlineExamService>();` if you want the
student online-exam endpoints to work.
