namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Generates sequential student codes for a teacher's account.
/// REQ-STU-007: Format is [Letter][Number] — A1, A2 ... A999, B1 ... Z999.
/// REQ-STU-008: Used when TeacherConfiguration.StudentCodeGenerationMode == Auto.
/// REQ-STU-009: Automatically assigns the next available code upon saving.
/// 
/// The sequence is per-teacher and always advances forward (no gap filling).
/// If the highest existing code is B14, the next generated code is B15.
/// </summary>
public interface IStudentCodeGenerator
{
    /// <summary>
    /// Generates the next sequential student code for the given teacher.
    /// Queries the highest existing code and computes the next one.
    /// The generated code is guaranteed to be unique within the teacher's account.
    /// </summary>
    /// <param name="teacherId">The teacher's Id (multi-tenant scope).</param>
    /// <returns>The next available student code (e.g., "A1", "B15", "Z999").</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the code space is exhausted (beyond Z999 = 25,974 codes).
    /// </exception>
    Task<string> GenerateNextCodeAsync(long teacherId);
}