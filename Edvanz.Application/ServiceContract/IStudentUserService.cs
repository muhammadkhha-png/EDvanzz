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
    /// Links a Teacher to the Student's dashboard by validating all three credentials.
    /// AAM-FR-05.5: TeacherCode + StudentCode + HashedToken must all match.
    /// AAM-FR-05.6: On success, the teacher entry appears on the dashboard.
    /// AAM-BR-02: Student cannot view teacher data until at least one link exists.
    /// </summary>
    /// <param name="studentUserId">The StudentUser's Id.</param>
    /// <param name="dto">The three required linking credentials.</param>
    /// <returns>Result containing the newly linked teacher's dashboard entry.</returns>
    Task<Result<StudentDashboardTeacherDto>> LinkTeacherAsync(long studentUserId, LinkTeacherDto dto);

    /// <summary>
    /// Removes a Teacher from the Student's dashboard (soft-unlink).
    /// Sets LinkStatus to Unlinked and records UnlinkedAt timestamp.
    /// The link record is preserved for audit purposes.
    /// </summary>
    /// <param name="studentUserId">The StudentUser's Id.</param>
    /// <param name="teacherId">The Teacher's Id to unlink.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result<bool>> UnlinkTeacherAsync(long studentUserId, long teacherId);

    /// <summary>
    /// Retrieves all teachers currently linked to the student's dashboard.
    /// AAM-FR-05.7: Each Teacher displayed distinctly.
    /// AAM-FR-05.8: Visibility governed by teacher's configuration.
    /// Only returns active links (LinkStatus = Active).
    /// </summary>
    /// <param name="studentUserId">The StudentUser's Id.</param>
    /// <returns>Result containing the list of linked teacher DTOs.</returns>
    Task<Result<List<StudentDashboardTeacherDto>>> GetLinkedTeachersAsync(long studentUserId);

    /// <summary>
    /// Retrieves a student user by their unique StudentAccountCode.
    /// Used by the Parent module when linking to a child's account (AAM-FR-06.3 Method A).
    /// Returns minimal info: account code, full name.
    /// </summary>
    /// <param name="accountCode">The unique student account code.</param>
    /// <returns>Result containing basic student info if found.</returns>
    Task<Result<StudentUserProfileDto>> GetStudentUserByAccountCodeAsync(string accountCode);


}