using Edvanz.Domain.Enums;

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

    /// <summary>
    /// Child's date of birth, captured as the Parent's own record. Required for BOTH creation
    /// methods (Parent Module requirements §7) — nullable here only so the service can detect
    /// "not provided" and return a validation error rather than silently persisting a default date.
    /// </summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// Child's gender. Required for BOTH creation methods, same rationale as <see cref="DateOfBirth"/>.
    /// </summary>
    public Gender? Gender { get; set; }
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

    /// <summary>
    /// Child's date of birth. Required (Parent Module requirements §7) — nullable here only so
    /// the service can detect "not provided" and return a validation error.
    /// </summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// Child's gender. Required, same rationale as <see cref="DateOfBirth"/>.
    /// </summary>
    public Gender? Gender { get; set; }
}
