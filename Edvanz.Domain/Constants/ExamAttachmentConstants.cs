namespace Edvanz.Domain.Constants;

/// <summary>
/// Constants for the OFFLINE exam's paper — the PDF or photos a teacher uploads so students
/// can review the questions after they have sat it.
///
/// Files themselves live in the central <c>FileObject</c> registry under
/// <c>FileCategory.ExamAttachment</c>; size and type limits are
/// <see cref="UploadConstants"/>'s and are not restated here. What IS specific to an exam is
/// WHEN the paper may be seen — see <see cref="DefaultReleaseDelayHours"/>.
/// </summary>
public static class ExamAttachmentConstants
{
    /// <summary>
    /// Maximum papers on one exam. Matches <see cref="UploadConstants.MaxFilesPerRequest"/> so
    /// a single multi-select upload can never exceed the exam's own cap and half-succeed.
    /// </summary>
    public const int MaxAttachmentsPerExam = 10;

    /// <summary>
    /// Fallback for <c>TeacherConfiguration.ExamAttachmentReleaseDelayHours</c> when no
    /// configuration row exists: how long after the LAST class sits the exam before the paper
    /// opens to students. 48 hours.
    ///
    /// Used as the fallback in the authorization path specifically because it is the SAFE
    /// direction — a missing configuration must delay a release, never bring one forward.
    /// </summary>
    public const int DefaultReleaseDelayHours = 48;

    /// <summary>
    /// Localization message keys (names match entries in <c>Messages.en.resx</c> /
    /// <c>Messages.ar.resx</c>). Service code references these constants — never inline
    /// literals — so a typo is a build failure, not a runtime miss.
    /// </summary>
    public static class Messages
    {
        public const string TooManyAttachments  = "ExamTooManyAttachments";   // 422 — over MaxAttachmentsPerExam
        public const string AttachmentNotFound  = "ExamAttachmentNotFound";   // 404
        public const string AttachmentsUpdated  = "ExamAttachmentsUpdated";   // 200
        public const string AttachmentRemoved   = "ExamAttachmentRemoved";    // 200
        public const string ReleaseUpdated      = "ExamAttachmentReleaseUpdated"; // 200
    }
}
