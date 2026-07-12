# Video Content Management (Module 14) — Phase 1: Domain & EF Foundation

> Handoff spec for Claude Code. Execute **directly in the repo**. Read every referenced
> file before editing. This phase touches Domain, Infrastructure/Persistence, and
> Domain/Constants only — no controllers, no services yet.

## Context

This is phase 1 of a 5-phase epic reshaping Video Content Management (Module 14):

1. **Phase 1 (this file)** — Domain entities, EF config, migration, constants, localization keys.
2. Phase 2 — Repository + shared service logic (unit-link replace, scope-replace extraction).
3. Phase 3 — `POST /api/videos` rework to multipart (metadata + thumbnail + PDF attachment in one call).
4. Phase 4 — `PUT /api/videos/{id}` extended (unitIds[], folded-in scope replace, manual duration
   override, optimistic concurrency, audit snapshot).
5. Phase 5 — `PUT /api/videos/{id}/thumbnail` (replace-only) + PDF-only attachment enforcement.

**Do not skip ahead.** Phases 3–5 depend on the schema and constants this phase produces.

### Confirmed product decisions driving this epic

- Video↔Unit relationship changes from 1:N (`VideoAsset.UnitId` scalar FK) to **M:N** — a video can
  belong to multiple units. Access = union of the video's own scope OR any linked unit's scope.
- `PUT /api/videos/{id}` **already exists** (`VideoService.UpdateVideoAsync`) and already handles
  title/description/sourceUrl/publishDate/status/unitId, including the confirmed rule that a
  changed `SourceUrl` resets `VideoAnalytics`/`VideoWatchEvent` and zeroes `DurationSeconds`
  (`ResetAnalyticsForVideoAsync`). **Do not re-implement this endpoint from scratch in Phase 4** —
  extend it.
- Teachers can now set an explicit `DurationSeconds` on update. Because `DurationSeconds` is
  currently purely student-reported (first report wins, later reports must fall within ±5%
  tolerance — `TryUpdateDurationWithinToleranceAsync`), a teacher-set value needs to be
  distinguishable from an unset one so StartWatch doesn't silently clobber it.
- Thumbnails are **uploaded images** (not URLs), stored in Azure Blob Storage exactly like
  `VideoAttachment` (DB stores blob path only; SAS read URL generated per request via
  `IFileStorageService.GetReadUrlAsync`, never persisted).
- Thumbnail upload happens inline during `POST /api/videos` (multipart, single request). A
  separate `PUT /api/videos/{id}/thumbnail` exists for **replace only**, using this order:
  upload new blob → update DB reference → delete old blob only after the DB update commits
  (prevents ever pointing the DB at a missing blob, and preserves the old thumbnail if either
  step fails).
- Attachments remain PDF-only (`application/pdf`), 25 MB cap (ratified, no longer "proposed").

## Files to read first (authoritative — do not guess names/shapes)

- `Edvanz.Domain/Entities/VideoAsset.cs` — full current entity. Note the XML doc block claiming
  "IMMUTABILITY... no edit endpoint" — this is **stale**; `PUT /api/videos/{id}` exists. Correct
  this doc comment as part of this phase (see Step 1).
- `Edvanz.Domain/Entities/VideoUnit.cs` — current unit entity.
- `Edvanz.Domain/Entities/VideoAttachment.cs` — mirror this entity's shape/conventions
  (`BlobPath`, `ContentType`, `FileSizeBytes`, tenant-scoped `TeacherId`) for the new thumbnail
  column naming.
- `Edvanz.Infrastructure/Persistence/EdvanzDbContext.cs` — `OnModelCreating` fluent config for
  `VideoAsset`, `VideoUnit`, `VideoAttachment`, and the `DbSet<>` declarations block for Module 14
  (search for "REQUIRED DbSet<> ADDITIONS — VIDEO CONTENT MANAGEMENT MODULE").
- `Edvanz.Domain/Constants/VideoConstants.cs` — full file. Note `AttachmentMaxSizeBytes` currently
  carries a "proposed default; confirm with product" comment — remove that qualifier, it is now
  ratified. Note the `Messages` nested class structure — new keys go here, following the existing
  naming pattern (`AttachmentUploaded`, `AttachmentTooLarge`, etc.).
- `Edvanz.Domain/Resources/Messages.en.resx` and `Messages.ar.resx` — confirm exact file names/path
  on disk before editing (do not assume `Messages_en.resx` — verify).
- Latest migration under `Edvanz.Infrastructure/Migrations/` — confirm the current migration chain
  head before adding a new one.

## Step 1 — Correct the stale immutability doc comment on `VideoAsset`

The class-level XML doc says:

> "IMMUTABILITY: Title, description, and source URL are set once at creation and never change
> afterwards. There is no edit endpoint and no `UpdatedAt` column (Q2(a) decision)."

Replace this paragraph. `PUT /api/videos/{id}` exists and title/description/sourceUrl/publishDate/
status/unitIds are all editable; as of this epic, `DurationSeconds` is also teacher-editable
(with the manual-override flag from Step 3) and an `UpdatedAt` column is being added. Rewrite the
paragraph to state the current rule accurately: everything is editable via `PUT /api/videos/{id}`
except `TeacherId`/`CreatedByUserId`/`Id`; a changed `SourceUrl` resets watch analytics as a
"different video" (existing rule, unchanged); `UpdatedAt` now tracks the last edit.

## Step 2 — Video↔Unit: 1:N → M:N

**Remove:**
- `VideoAsset.UnitId` (property + FK)
- `VideoAsset.Unit` navigation property
- The corresponding `HasOne(...).WithMany(...)` fluent config in `OnModelCreating`

**Add new entity** `VideoAssetUnit` (new file `Edvanz.Domain/Entities/VideoAssetUnit.cs`):

```csharp
public class VideoAssetUnit
{
    public long VideoAssetId { get; set; }
    public VideoAsset VideoAsset { get; set; } = null!;

    public long UnitId { get; set; }
    public VideoUnit Unit { get; set; } = null!;
}
```

Match the existing project convention: no `[ForeignKey]` data annotations (Fluent API is the
sole source of truth per the documented EF Core 10 silent-`OnDelete`-drop bug). Composite key
`(VideoAssetId, UnitId)`.

**Fluent API** (add to `OnModelCreating`, alongside the existing `VideoAsset`/`VideoUnit` config):

```csharp
modelBuilder.Entity<VideoAssetUnit>(b =>
{
    b.HasKey(x => new { x.VideoAssetId, x.UnitId });

    b.HasOne(x => x.VideoAsset)
     .WithMany(v => v.AssetUnits) // add this collection nav to VideoAsset — see below
     .HasForeignKey(x => x.VideoAssetId)
     .OnDelete(DeleteBehavior.NoAction);

    b.HasOne(x => x.Unit)
     .WithMany(u => u.AssetUnits) // add this collection nav to VideoUnit
     .HasForeignKey(x => x.UnitId)
     .OnDelete(DeleteBehavior.NoAction);
});
```

Add `public ICollection<VideoAssetUnit> AssetUnits { get; set; } = new List<VideoAssetUnit>();`
to both `VideoAsset` and `VideoUnit`. NoAction on both sides matches the existing posture for
every other Module 14 FK (scopes, analytics, watch events) — the service layer owns cleanup,
not cascade.

**Add `DbSet<VideoAssetUnit> VideoAssetUnits => Set<VideoAssetUnit>();`** to `EdvanzDbContext`,
next to the other Module 14 DbSets.

## Step 3 — New columns on `VideoAsset`

```csharp
/// <summary>Last update timestamp. Set on every PUT /api/videos/{id} call.</summary>
public DateTime? UpdatedAt { get; set; }

/// <summary>
/// Optimistic concurrency token. PUT /api/videos/{id} now spans multiple related
/// writes (fields, unit links, optionally scopes) — this guards against two
/// concurrent editors (e.g., teacher + assistant) silently overwriting each other.
/// Same pattern as TeacherSubscription.RowVersion / AssignmentTemplate.RowVersion.
/// </summary>
[Timestamp]
public byte[] RowVersion { get; set; } = null!;

/// <summary>
/// Blob path to the uploaded thumbnail image, or null if none set. Same convention
/// as VideoAttachment.BlobPath — canonical reference stored in DB; SAS read URL
/// generated per request via IFileStorageService.GetReadUrlAsync, never persisted.
/// </summary>
[MaxLength(500)]
public string? ThumbnailBlobPath { get; set; }

/// <summary>
/// True once a teacher has explicitly set DurationSeconds via PUT /api/videos/{id}.
/// When true, StartWatch's first-report-wins logic must NOT overwrite the value —
/// subsequent student reports are tolerance-checked against it instead, same as the
/// existing non-zero-duration branch in TryUpdateDurationWithinToleranceAsync.
/// </summary>
public bool IsDurationManuallySet { get; set; } = false;
```

Add `[Timestamp]` and `System.ComponentModel.DataAnnotations` / `.Schema` usings as needed,
matching the pattern in `TeacherSubscription.cs` or `AssignmentTemplate.cs`.

## Step 4 — Constants (`VideoConstants.cs`)

Add:

```csharp
/// <summary>
/// Maximum size of a single thumbnail image upload. 5 MB — proposed default;
/// confirm with product before relying on this as a hard business rule.
/// (Same caveat pattern as AttachmentMaxSizeBytes carried before ratification.)
/// </summary>
public const long ThumbnailMaxSizeBytes = 5 * 1024 * 1024;

/// <summary>Allowed content types for thumbnail uploads.</summary>
public static readonly string[] AllowedThumbnailContentTypes = { "image/jpeg", "image/png" };

/// <summary>Allowed content types for video attachments — PDF only (ratified).</summary>
public static readonly string[] AllowedAttachmentContentTypes = { "application/pdf" };
```

Remove the "proposed default; confirm with product before this is relied on as a hard business
rule" comment on `AttachmentMaxSizeBytes` — replace with "Ratified business rule (25 MB)."

Add new message keys to the `Messages` nested class, grouped with the existing
`AttachmentUploaded`/`AttachmentDeleted`/`AttachmentNotFound`/`AttachmentTooLarge` block:

```csharp
public const string AttachmentInvalidType = "AttachmentInvalidType";
public const string ThumbnailUploaded     = "ThumbnailUploaded";
public const string ThumbnailReplaced     = "ThumbnailReplaced";
public const string ThumbnailInvalidType  = "ThumbnailInvalidType";
public const string ThumbnailTooLarge     = "ThumbnailTooLarge";
public const string ThumbnailNotFound     = "ThumbnailNotFound";
```

## Step 5 — Localization

Add English and Egyptian-Arabic entries for all six new keys to `Messages.en.resx` /
`Messages.ar.resx` (confirm exact file paths first — do not assume). Follow the tone/phrasing
style of the existing `Attachment*` entries in the same files. Example English values (adjust to
match house style found in the existing entries):

- `AttachmentInvalidType` → "Only PDF files are allowed for attachments."
- `ThumbnailUploaded` → "Thumbnail uploaded successfully."
- `ThumbnailReplaced` → "Thumbnail replaced successfully."
- `ThumbnailInvalidType` → "Only JPEG or PNG images are allowed for thumbnails."
- `ThumbnailTooLarge` → "Thumbnail exceeds the maximum allowed size."
- `ThumbnailNotFound` → "No thumbnail found for this video."

Egyptian-Arabic phrasing: mirror the register of existing `Attachment*` Arabic entries in the
same `.resx` — do not invent a different tone.

## Step 6 — Migration

```bash
dotnet ef migrations add VCM_ManyToManyUnits_Thumbnail_Concurrency \
  --project Edvanz.Infrastructure --startup-project Edvanz.API
```

**Backfill in the migration's `Up()` method** (raw SQL, after the schema changes, before dropping
the old column): for every `VideoAssets` row with a non-null `UnitId`, insert one row into
`VideoAssetUnits`. Then drop the `UnitId` column. Something like:

```sql
INSERT INTO VideoAssetUnits (VideoAssetId, UnitId)
SELECT Id, UnitId FROM VideoAssets WHERE UnitId IS NOT NULL;
```//run before `DropColumn` for `UnitId` in the same migration.

Do **not** hand-edit `migrate.sql` at repo root — it's generated CI output, not source.

**Build** the solution; fix any compile errors from the removed `UnitId`/`Unit` references
(there will be some in `VideoDtos.cs` and `VideoService.cs` — leave those for Phase 2–4; for
Phase 1, only Domain + Infrastructure + `EdvanzDbContext` need to compile cleanly. If
`VideoService.cs`/`VideoDtos.cs` fail to compile because they reference `UnitId`, that's
expected — Phase 2–4 fixes them. Confirm this is the case rather than papering over it.)

## Acceptance checklist

- [ ] `VideoAsset.UnitId`/`Unit` removed; `VideoAssetUnit` join entity added with composite key,
      both FKs `NoAction`, no `[ForeignKey]` annotations.
- [ ] `VideoAsset` gains `UpdatedAt`, `RowVersion` (`[Timestamp]`), `ThumbnailBlobPath`,
      `IsDurationManuallySet`.
- [ ] Stale immutability doc comment on `VideoAsset` rewritten to reflect the actual edit rules.
- [ ] `VideoConstants`: `ThumbnailMaxSizeBytes`, `AllowedThumbnailContentTypes`,
      `AllowedAttachmentContentTypes` added; `AttachmentMaxSizeBytes` comment updated to
      "ratified."
- [ ] Six new message keys added to `VideoConstants.Messages` and both `.resx` files.
- [ ] Migration created and includes the `UnitId` → `VideoAssetUnits` backfill **before**
      dropping the column.
- [ ] `Edvanz.Domain` and `Edvanz.Infrastructure` build clean. `Edvanz.Application`/`Edvanz.API`
      compile errors from the removed `UnitId` are expected and left for Phase 2+ — confirm and
      list them in the report, don't silently patch them here.
- [ ] No other module touched.

## Report back

Summary of work; list of created/modified files; the exact list of `Edvanz.Application`/
`Edvanz.API` compile errors now present (needed to scope Phase 2); confirmed `.resx` file paths
used; any schema/naming deviations from this spec and why.

---

## How to run this with Claude Code

1. Install (Node.js required, one-time): `npm install -g @anthropic-ai/claude-code`
2. Open a terminal at the repo root (folder containing `CLAUDE.md`).
3. Save this file as `docs/vcm-phase1-foundation-domain-ef-constants.md` and commit it.
4. Start Claude Code (`claude`), then:
   > Read docs/vcm-phase1-foundation-domain-ef-constants.md and CLAUDE.md, then execute this
   > phase exactly as specified. Read every referenced file before editing. Stop at the
   > acceptance checklist and report back — do not proceed into Phase 2 work.
5. Review the diff before accepting. Run `dotnet build Edvanz.slnx`, confirm Domain/Infrastructure
   compile clean, and check the migration's generated SQL (`dotnet ef migrations script`) before
   applying it to a database with real data.
