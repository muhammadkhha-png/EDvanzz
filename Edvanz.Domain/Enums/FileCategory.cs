namespace Edvanz.Domain.Enums;

/// <summary>
/// What a <see cref="Entities.FileObject"/> is, which selects its authorization policy in
/// <c>IFileAccessService</c>. Numeric values are part of the persisted contract — never reorder.
/// </summary>
public enum FileCategory : byte
{
    /// <summary>A video's cover thumbnail. Teacher/assistant-scoped.</summary>
    VideoThumbnail = 1,

    /// <summary>A PDF (or other file) attached to a video. Teacher/assistant-scoped.</summary>
    VideoAttachment = 2,

    /// <summary>An image on an online-exam question. Teacher/assistant OR a student assigned to the exam.</summary>
    OnlineExamQuestionImage = 3,

    /// <summary>An image on a video-attached-exam question. Teacher/assistant-scoped.</summary>
    VideoExamQuestionImage = 4,

    /// <summary>The national-ID image captured at sign-up. Owner + SuperAdmin only (no resource policy).</summary>
    NationalIdImage = 5,
}
