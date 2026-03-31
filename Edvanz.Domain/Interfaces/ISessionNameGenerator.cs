using Edvanz.Domain.Enums;

namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Generates sequential session names for a teacher's account.
/// REQ-SES-002: Auto-generation pattern: Session A1, Session A2 … Session B1, Session B2 …
/// REQ-SES-004: Language determined by TeacherConfiguration.SessionNameLanguage.
/// 
/// English sequence: Session A1 → Session A2 → … → Session A999 → Session B1 → … → Session Z999
/// Arabic sequence:  حصة أ1 → حصة أ2 → … → حصة أ999 → حصة ب1 → … → حصة ي999
/// 
/// The sequence is per-teacher and always advances forward (no gap filling),
/// mirroring the StudentCodeGenerator pattern.
/// </summary>
public interface ISessionNameGenerator
{
    /// <summary>
    /// Generates the next sequential session name for the given teacher
    /// in the specified language.
    /// Queries the highest existing auto-generated session name and computes the next one.
    /// </summary>
    /// <param name="teacherId">The teacher's Id (multi-tenant scope).</param>
    /// <param name="language">
    /// The language for name generation (AAM-FR-04.3).
    /// English: "Session A1". Arabic: "حصة أ1".
    /// Defaults to English for backward compatibility.
    /// </param>
    /// <returns>The next available session name (e.g., "Session A1", "حصة ب15").</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the name space is exhausted (beyond Session Z999 / حصة ي999).
    /// </exception>
    Task<string> GenerateNextNameAsync(long teacherId, GenerationLanguage language = GenerationLanguage.English);
}