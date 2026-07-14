namespace Edvanz.Domain.Enums;

/// <summary>
/// Identifies who is viewing a student's payment data, so the Payment service can enforce
/// the correct <c>TeacherConfiguration</c> visibility flag
/// (<c>StudentVisibilityPayment</c> vs <c>ParentVisibilityPayment</c>).
/// Mirrors <see cref="AttendanceViewerType"/>.
/// </summary>
public enum PaymentViewerType : byte
{
    Student = 1,
    Parent = 2
}
