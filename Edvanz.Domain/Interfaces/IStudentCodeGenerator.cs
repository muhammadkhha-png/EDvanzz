using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Generates sequential student codes for a teacher's account.
/// REQ-STU-007: Format is [Letter][Number] — A1, A2 ... A999, B1 ... Z999.
/// REQ-STU-008: Used when TeacherConfiguration.StudentCodeGenerationMode == Auto.
/// REQ-STU-009: Automatically assigns the next available code upon saving.
/// AAM-FR-04.2: Codes are generated in Arabic or English per the teacher's
///              StudentCodeLanguage configuration setting.
/// 
/// English sequence: A1 → A2 → ... → A999 → B1 → ... → Z999
/// Arabic sequence:  أ1 → أ2 → ... → أ999 → ب1 → ... → ي999
/// 
/// The sequence is per-teacher and always advances forward (no gap filling).
/// If the highest existing code is B14, the next generated code is B15.
/// </summary>
public interface IStudentCodeGenerator
{
    /// <summary>
    /// Generates the next sequential student code for the given teacher
    /// in the specified language.
    /// Queries the highest existing code and computes the next one.
    /// The generated code is guaranteed to be unique within the teacher's account.
    /// </summary>
    /// <param name="teacherId">The teacher's Id (multi-tenant scope).</param>
    /// <param name="language">
    /// The language for code generation (AAM-FR-04.2).
    /// English: A-Z letter prefix. Arabic: أ-ي letter prefix.
    /// Defaults to English for backward compatibility.
    /// </param>
    /// <returns>The next available student code (e.g., "A1", "ب15").</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the code space is exhausted (beyond Z999 / ي999).
    /// </exception>
    Task<string> GenerateNextCodeAsync(long teacherId, GenerationLanguage language = GenerationLanguage.English);

    /// <summary>
    /// Generates <paramref name="count"/> sequential codes in a SINGLE database read, incrementing
    /// in memory. Use this for bulk operations instead of calling <see cref="GenerateNextCodeAsync"/>
    /// per row: that re-queries the DB every call, and because the freshly generated codes are not
    /// yet persisted it keeps returning the same value — spinning and hammering the DB. The returned
    /// codes are contiguous and unique among themselves; the caller must still skip any that collide
    /// with codes supplied elsewhere in the same batch.
    /// </summary>
    /// <param name="teacherId">The teacher's Id (multi-tenant scope).</param>
    /// <param name="count">How many sequential codes to produce.</param>
    /// <param name="language">Code language (AAM-FR-04.2).</param>
    Task<List<string>> GenerateSequentialCodesAsync(long teacherId, int count, GenerationLanguage language = GenerationLanguage.English);
}