namespace Edvanz.Domain.Enums;

/// <summary>
/// What a <see cref="Entities.FileObject"/> is, which selects its authorization policy in
/// <c>IFileAccessService</c>. Numeric values are part of the persisted contract — never reorder.
/// </summary>
public enum FileCategory : byte
{
    /// <summary>A video's cover photo (formerly "thumbnail"). Teacher/assistant OR a student the video is scoped to.</summary>
    VideoPhoto = 1,

    /// <summary>A PDF (or other file) attached to a video. Teacher/assistant OR a student the video is scoped to.</summary>
    VideoAttachment = 2,

    /// <summary>An image on an online-exam question. Teacher/assistant OR a student assigned to the exam.</summary>
    OnlineExamQuestionImage = 3,

    /// <summary>An image on a video-attached-exam question. Teacher/assistant OR a student the video is scoped to.</summary>
    VideoExamQuestionImage = 4,

    /// <summary>The national-ID image captured at sign-up. Owner + SuperAdmin only (no resource policy).</summary>
    NationalIdImage = 5,

    /// <summary>
    /// The paper (PDF or photos) of an OFFLINE exam, so students can review the questions
    /// afterwards. Teacher/assistant tenant at any time; a student only once the exam's
    /// release gate has opened — see
    /// <c>IExamHomeworkRepo.IsExamAttachmentVisibleToStudentAsync</c>.
    /// </summary>
    ExamAttachment = 6,
}
