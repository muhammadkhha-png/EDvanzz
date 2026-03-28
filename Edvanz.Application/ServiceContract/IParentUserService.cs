using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.ParentUser;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Defines the contract for Parent User module operations.
/// Called by the User module after routing based on UserType = Parent.
/// 
/// IMPORTANT: User-level operations (registration, login, password update,
/// account deactivation, deletion) are handled by the User module.
/// This service handles Parent-SPECIFIC operations only.
/// </summary>
public interface IParentUserService
{
    /// <summary>
    /// Initializes the ParentUser record after the User record is created.
    /// </summary>
    Task<Result<ParentUserProfileDto>> InitializeParentUserAsync(CreateParentUserDto dto);

    /// <summary>
    /// Retrieves the full parent user profile including child count.
    /// </summary>
    Task<Result<ParentUserProfileDto>> GetParentUserProfileAsync(long parentUserId);

    /// <summary>
    /// Updates the parent user's profile settings (language preference).
    /// </summary>
    Task<Result<ParentUserProfileDto>> UpdateParentUserProfileAsync(long parentUserId, UpdateParentUserProfileDto dto);

    /// <summary>
    /// Retrieves the parent's dashboard with all children and their linked teachers.
    /// AAM-FR-06.4: Shows all children. AAM-FR-06.6: Visibility per teacher's config.
    /// </summary>
    Task<Result<ParentDashboardDto>> GetDashboardAsync(long parentUserId);

    /// <summary>
    /// Method A: Links a child who HAS a Student User account to this parent.
    /// AAM-FR-06.3 Method A: Parent scans/enters the StudentAccountCode.
    /// Upon success, parent can view all teachers already linked to that student.
    /// </summary>
    Task<Result<ParentChildDto>> AddChildByAccountCodeAsync(long parentUserId, AddChildByAccountCodeDto dto);

    /// <summary>
    /// Method B: Creates a manual child profile (child has no Student User account).
    /// AAM-FR-06.3 Method B: Parent enters the child's name.
    /// Teachers are added separately via LinkTeacherToChildAsync.
    /// </summary>
    Task<Result<ParentChildDto>> AddChildManualAsync(long parentUserId, AddChildManualDto dto);

    /// <summary>
    /// Method B only: Links a Teacher to a manual child profile using the 3 credentials.
    /// Same validation as AAM-FR-05.5 (TeacherCode + StudentCode + HashedToken).
    /// NOT used for Method A children — their teachers come from StudentTeacherLink.
    /// </summary>
    Task<Result<ParentChildTeacherDto>> LinkTeacherToChildAsync(long parentUserId, long childId, LinkTeacherToChildDto dto);

    /// <summary>
    /// Method B only: Removes a Teacher from a manual child profile (soft-unlink).
    /// </summary>
    Task<Result<bool>> UnlinkTeacherFromChildAsync(long parentUserId, long childId, long teacherId);

    /// <summary>
    /// Retrieves a single child with their linked teachers.
    /// </summary>
    Task<Result<ParentChildDto>> GetChildAsync(long parentUserId, long childId);

    /// <summary>
    /// Removes a child from the parent's account (soft-deactivate).
    /// </summary>
    Task<Result<bool>> RemoveChildAsync(long parentUserId, long childId);
}