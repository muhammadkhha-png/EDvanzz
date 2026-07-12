using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.StudentUser;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Defines the contract for Student User module operations.
/// Called by the User module after routing based on UserType = Student.
/// All methods are async per system architecture requirements.
/// Lives in the Application layer because it depends on Application DTOs.
/// 
/// IMPORTANT: User-level operations (registration, login, password update,
/// account deactivation, deletion) are handled by the User module.
/// This service handles Student-SPECIFIC operations only.
/// </summary>
public interface IStudentUserService
{
    /// <summary>
    /// Initializes the StudentUser record and generates the StudentAccountCode.
    /// Called after the User record is created during registration.
    /// AAM-FR-05.3: Auto-generates unique StudentAccountCode.
    /// AAM-FR-05.4: Sets IsFirstLogin = true for empty dashboard prompt.
    /// </summary>
    /// <param name="dto">Registration data including userId and language preference.</param>
    /// <returns>Result containing the created student user's profile including the generated code.</returns>
    Task<Result<StudentUserProfileDto>> InitializeStudentUserAsync(CreateStudentUserDto dto);

    /// <summary>
    /// Retrieves the full student user profile including linked teacher count.
    /// </summary>
    /// <param name="studentUserId">The StudentUser's Id (not the UserId).</param>
    /// <returns>Result containing the student user profile DTO.</returns>
    Task<Result<StudentUserProfileDto>> GetStudentUserProfileAsync(long studentUserId);

    /// <summary>
    /// Updates the student user's own profile settings.
    /// AAM-FR-02.3: Language preference changeable from settings.
    /// Note: FullName, PhoneNumber, Password updates go through the User module.
    /// </summary>
    /// <param name="studentUserId">The StudentUser's Id.</param>
    /// <param name="dto">Profile fields to update.</param>
    /// <returns>Result containing the updated student user profile.</returns>
    Task<Result<StudentUserProfileDto>> UpdateStudentUserProfileAsync(long studentUserId, UpdateStudentUserProfileDto dto);

    /// <summary>
    /// Retrieves the student's dashboard including linked teachers and first-login state.
    /// AAM-FR-05.4: Empty dashboard with prompt on first login.
    /// AAM-FR-05.6/07: Lists all linked teachers with name and subject.
    /// AAM-FR-05.8: Includes visibility configuration per teacher.
    /// </summary>
    /// <param name="studentUserId">The StudentUser's Id.</param>
    /// <returns>Result containing the dashboard DTO.</returns>
    Task<Result<StudentDashboardDto>> GetDashboardAsync(long studentUserId);

    /// <summary>
    /// Sends a link REQUEST to a teacher (request/approval flow — supersedes the
    /// 3-credential instant link). Creates a Pending StudentTeacherLink carrying
    /// the student-typed name and optional teacher-assigned student code; the
    /// teacher later accepts (binding a roster record) or rejects it.
    /// Notifies the teacher (inbox + push) post-commit, best-effort.
    /// </summary>
    /// <param name="studentUserId">The StudentUser's Id (resolved from JWT by the controller).</param>
    /// <param name="dto">Teacher code + student-typed name + optional student code.</param>
    /// <returns>Result containing the new Pending dashboard entry for the teacher.</returns>
    Task<Result<StudentDashboardTeacherDto>> CreateLinkRequestAsync(long studentUserId, CreateLinkRequestDto dto);

    /// <summary>
    /// Removes a Teacher from the Student's side:
    ///   - a Pending request is cancelled (LinkStatus = CancelledByStudent);
    ///   - an Active link is unlinked (LinkStatus = Unlinked).
    /// Terminal rows are preserved for audit; the student may send a new request later.
    /// </summary>
    /// <param name="studentUserId">The StudentUser's Id (resolved from JWT by the controller).</param>
    /// <param name="teacherId">The Teacher's Id to remove.</param>
    /// <param name="actingUserId">User.Id of the caller — persisted to RemovedByUserId for audit.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result<bool>> UnlinkTeacherAsync(long studentUserId, long teacherId, long actingUserId);

    /// <summary>
    /// Retrieves the student's teachers INCLUDING request states — one entry per
    /// teacher (the latest link row), so the student can see Pending requests and
    /// whether a request was accepted (Active) or rejected (Rejected).
    /// AAM-FR-05.7: Each Teacher displayed distinctly.
    /// AAM-FR-05.8: Visibility flags from the teacher's configuration are included
    /// so the app knows which module tiles to show once the link is Active.
    /// </summary>
    /// <param name="studentUserId">The StudentUser's Id (resolved from JWT by the controller).</param>
    /// <returns>Result containing the list of teacher entries with status.</returns>
    Task<Result<List<StudentDashboardTeacherDto>>> GetMyTeachersAsync(long studentUserId);

    /// <summary>
    /// Retrieves a student user by their unique StudentAccountCode.
    /// Used by the Parent module when linking to a child's account (AAM-FR-06.3 Method A).
    /// Returns minimal info: account code, full name.
    /// </summary>
    /// <param name="accountCode">The unique student account code.</param>
    /// <returns>Result containing basic student info if found.</returns>
    Task<Result<StudentUserProfileDto>> GetStudentUserByAccountCodeAsync(string accountCode);


}