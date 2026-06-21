namespace Edvanz.Domain.Enums;

/// <summary>
/// Identifies who is reading a student's attendance through the student/parent
/// view endpoints, so the service enforces the correct TeacherConfiguration flag.
/// Student visibility (AAM-FR-04.8) and parent visibility (AAM-FR-04.9) are
/// independent toggles. Byte-backed with locked values per project convention.
/// </summary>
public enum AttendanceViewerType : byte
{
    /// <summary>Student viewing their own attendance. Gated by StudentVisibilityAttendance.</summary>
    Student = 1,

    /// <summary>Parent viewing a linked child's attendance. Gated by ParentVisibilityAttendance.</summary>
    Parent = 2
}