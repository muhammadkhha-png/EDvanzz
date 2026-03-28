namespace Edvanz.Application.Dtos.ParentUser;

/// <summary>
/// Input DTO for Method A: linking a child who HAS a Student User account.
/// AAM-FR-06.3 Method A: Parent scans or enters the StudentAccountCode.
/// </summary>
public class AddChildByAccountCodeDto
{
    /// <summary>
    /// The child's unique StudentAccountCode (from StudentUser.StudentAccountCode).
    /// AAM-FR-05.3: The globally unique code generated when the student registered.
    /// </summary>
    public string StudentAccountCode { get; set; } = null!;
}

/// <summary>
/// Input DTO for Method B: creating a child profile manually (child has no account).
/// AAM-FR-06.3 Method B: Parent enters the child's name.
/// </summary>
public class AddChildManualDto
{
    /// <summary>
    /// The child's display name entered by the parent.
    /// Supports Arabic and English input.
    /// </summary>
    public string ChildName { get; set; } = null!;
}