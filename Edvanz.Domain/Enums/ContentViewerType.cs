namespace Edvanz.Domain.Enums;

/// <summary>
/// Identifies who is viewing a student's data through the student/parent read
/// surfaces (attendance, payment, and — as later phases wire them up — video,
/// online exam, offline exam, homework), so the owning service enforces the
/// correct <c>TeacherConfiguration</c> visibility flag pair
/// (<c>StudentVisibility*</c> vs <c>ParentVisibility*</c>).
///
/// Phase 3 (parent parity) consolidation: replaces the formerly-separate
/// <c>AttendanceViewerType</c> and <c>PaymentViewerType</c>, which were
/// structurally identical (same two members, same byte backing, same purpose)
/// and duplicated per-module rather than shared. One enum, one place to look,
/// one place to extend when a new content module gains a parent surface.
/// Byte-backed with locked values per project convention.
/// </summary>
public enum ContentViewerType : byte
{
    /// <summary>Student viewing their own data. Gated by the module's StudentVisibility* flag.</summary>
    Student = 1,

    /// <summary>Parent viewing a linked child's data. Gated by the module's ParentVisibility* flag.</summary>
    Parent = 2
}