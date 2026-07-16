using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Constants;

/// <summary>
/// Constants for the central file registry (<see cref="Entities.FileObject"/>) and its gated
/// endpoint. Mirrors the <see cref="UploadConstants"/> pattern — compile-time-checked message keys
/// and the set of categories that may be created through the authenticated upload endpoint.
/// </summary>
public static class FileConstants
{
    /// <summary>
    /// Categories a client may request when uploading through <c>POST /api/upload</c>. The
    /// national-ID image is excluded — it is created server-side during sign-up (there is no
    /// pre-account JWT to authorize a direct upload).
    /// </summary>
    public static readonly IReadOnlySet<FileCategory> UploadableCategories = new HashSet<FileCategory>
    {
        FileCategory.VideoPhoto,
        FileCategory.VideoAttachment,
        FileCategory.OnlineExamQuestionImage,
        FileCategory.VideoExamQuestionImage,
    };

    /// <summary>
    /// Localization message keys (names match entries in <c>Messages.en.resx</c> /
    /// <c>Messages.ar.resx</c>). Service code references these constants — never inline literals.
    /// </summary>
    public static class Messages
    {
        public const string InvalidCategory       = "FileInvalidCategory";        // 400 — missing/unknown category
        public const string CategoryNotUploadable = "FileCategoryNotUploadable";  // 400 — not allowed via /api/upload
        public const string NotFound              = "FileNotFound";               // 404
        public const string NotOwned              = "FileNotOwned";               // 403
        public const string CategoryMismatch      = "FileCategoryMismatch";       // 400 — wrong kind for the slot
        public const string AlreadyInUse          = "FileAlreadyInUse";           // 409 — attached elsewhere
    }
}
