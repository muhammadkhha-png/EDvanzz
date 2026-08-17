namespace Edvanz.Domain.Interfaces;

/// <summary>
/// Generates unique, collision-resistant 8-digit center codes (mirrors
/// <see cref="ITeacherCodeGenerator"/>). The code is permanent and immutable after creation and is
/// checked for uniqueness against the Centers table before being returned.
/// </summary>
public interface ICenterCodeGenerator
{
    /// <summary>Generates a unique 8-digit numeric center code that does not exist in the database.</summary>
    Task<string> GenerateUniqueCodeAsync();
}
