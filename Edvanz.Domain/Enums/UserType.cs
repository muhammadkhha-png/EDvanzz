namespace Edvanz.Domain.Enums;

/// <summary>
/// Defines the type of user account in the system.
/// AAM-FR-01.1: Teacher, Student, Parent, and Assistant.
/// SuperAdmin is added for the platform-level admin account (REQ-ADM-001).
/// Center / CenterAssistant are the multi-teacher tenancy tier (a Center owns several
/// Teachers and "acts as" any of them; the value drives the JWT Role claim via
/// <c>UserType.ToString()</c> in TokenService — "Center" / "CenterAssistant").
/// </summary>
public enum UserType
{
    Teacher = 1,
    Assistant = 2,
    SuperAdmin = 3,
    Student = 4,
    Parent = 5,
    Center = 6,
    CenterAssistant = 7
}