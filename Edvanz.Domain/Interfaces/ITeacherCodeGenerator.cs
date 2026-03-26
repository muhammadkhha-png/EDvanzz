namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Generates unique, collision-resistant 8-digit teacher codes.
/// AAM-FR-03.3: Auto-generated upon successful registration.
/// AAM-NFR-03: Cryptographically unique and collision-resistant.
/// AAM-BR-05: Code is permanent and immutable after creation.
/// </summary>
public interface ITeacherCodeGenerator
{
    /// <summary>
    /// Generates a unique 8-digit numeric teacher code that does not exist in the database.
    /// </summary>
    /// <returns>A unique 8-digit string (e.g., "48291057").</returns>
    Task<string> GenerateUniqueCodeAsync();
}