namespace Edvanz.Domain.Constants;

/// <summary>
/// Module name + permission + message-key constants for the Online Exam Module.
/// Single-sources strings across [ModulePermission] attributes, the permission
/// seeder, and the localizer — same pattern as VideoConstants/StudentConstants.
///
/// SEED DEPENDENCY (flagged, not code): "OnlineExam" must exist as a row in the
/// Modules seed table with permissions "ManageExams"/"View" before
/// [ModulePermission] checks against this module will ever succeed for an
/// Assistant. Module # assignment was left TBD by the plan — this uses the
/// module NAME string as the key (matches how VideoConstants/StudentConstants
/// work; no numeric module id appears in application code).
/// </summary>
public static class OnlineExamConstants
{
    public const string ModuleName = "OnlineExam";

    public const string PermissionManageExams = "ManageExams";
    public const string PermissionView = "View";

    public static class Messages
    {
        public const string Created = "OnlineExam.Created";
        public const string Updated = "OnlineExam.Updated";
        public const string Deleted = "OnlineExam.Deleted";
        public const string NotFound = "OnlineExam.NotFound";
        public const string Published = "OnlineExam.Published";
        public const string Closed = "OnlineExam.Closed";
        public const string InvalidStatusTransition = "OnlineExam.InvalidStatusTransition";
        public const string StartAfterEnd = "OnlineExam.StartAfterEnd";
        public const string PassPercentageOutOfRange = "OnlineExam.PassPercentageOutOfRange";
        public const string DegreeSumInvalid = "OnlineExam.DegreeSumInvalid";
        public const string SingleChoiceNeedsExactlyOneCorrect = "OnlineExam.SingleChoiceNeedsExactlyOneCorrect";
        public const string MultipleChoiceNeedsAtLeastOneCorrect = "OnlineExam.MultipleChoiceNeedsAtLeastOneCorrect";
        public const string QuestionsEditableOnlyInDraft = "OnlineExam.QuestionsEditableOnlyInDraft";
        public const string PublishRequiresQuestionAndScope = "OnlineExam.PublishRequiresQuestionAndScope";
        public const string ScopeTargetNotOwned = "OnlineExam.ScopeTargetNotOwned";
        public const string ConcurrencyConflict = "OnlineExam.ConcurrencyConflict";
        public const string TeacherSubjectNotOwned = "OnlineExam.TeacherSubjectNotOwned";
        public const string ScopeCannotBeEmpty = "OnlineExam.ScopeCannotBeEmpty";
        public const string MixedScopeTypesNotAllowed = "OnlineExam.MixedScopeTypesNotAllowed";
        public const string TitleRequired = "OnlineExam.TitleRequired";
        public const string DegreeMustBePositive = "OnlineExam.DegreeMustBePositive";
        public const string QuestionNeedsAtLeastTwoOptions = "OnlineExam.QuestionNeedsAtLeastTwoOptions";
        public const string NoQuestionsProvided = "OnlineExam.NoQuestionsProvided";
        public const string ExamNotDraft = "OnlineExam.ExamNotDraft";
        public const string UnpublishBlockedBySubmissions = "OnlineExam.UnpublishBlockedBySubmissions";
        public const string WindowClosed = "OnlineExam.WindowClosed";
        public const string NotInScope = "OnlineExam.NotInScope";
        public const string AlreadySubmitted = "OnlineExam.AlreadySubmitted";
        public const string StudentBlocked = "OnlineExam.StudentBlocked";
        public const string ReportNotFound = "OnlineExam.ReportNotFound";
        public const string InvalidOptionSelection = "OnlineExam.InvalidOptionSelection";
        public const string ManualStatusNotAllowed = "OnlineExam.ManualStatusNotAllowed";
        public const string CannotBlockFinalizedReport = "OnlineExam.CannotBlockFinalizedReport";
        public const string StudentNotOwned = "OnlineExam.StudentNotOwned";
    }
}