namespace Edvanz.Domain.Enums;

/// <summary>
/// Defines the type of user account in the system.
/// AAM-FR-01.1: Teacher, Student, Parent, and Assistant.
/// SuperAdmin is added for the platform-level admin account (REQ-ADM-001).
/// </summary>
public enum UserType
{
    Teacher = 1,
    Assistant = 2,
    SuperAdmin = 3,
    Student = 4,
    Parent = 5
}