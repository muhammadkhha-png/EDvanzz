namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Generates unique, collision-resistant student account codes.
/// AAM-FR-05.3: Auto-generated upon successful student registration.
/// AAM-NFR-03: Cryptographically unique and collision-resistant.
/// 
/// This generates the ACCOUNT-level code (globally unique across the platform),
/// not the teacher-assigned student code (TeacherStudent.StudentCode).
/// </summary>
public interface IStudentAccountCodeGenerator
{
    /// <summary>
    /// Generates a unique 10-character alphanumeric student account code
    /// that does not already exist in the database.
    /// Format: uppercase letters and digits (e.g., "STU4X7B2KM").
    /// </summary>
    /// <returns>A unique 10-character alphanumeric string.</returns>
    Task<string> GenerateUniqueCodeAsync();
}