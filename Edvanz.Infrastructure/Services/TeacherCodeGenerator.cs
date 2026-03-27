using System.Security.Cryptography;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// Generates unique 8-digit numeric teacher codes using a cryptographic random number generator.
/// AAM-NFR-03: Cryptographically unique and collision-resistant.
/// Performs a database uniqueness check before returning the code.
/// </summary>
public class TeacherCodeGenerator : ITeacherCodeGenerator
{
    private readonly EdvanzDbContext _context;

    /// <summary>
    /// Maximum number of generation attempts before throwing to prevent infinite loops.
    /// With 90M possible 8-digit codes (10000000–99999999), collisions are extremely rare.
    /// </summary>
    private const int MaxAttempts = 10;

    public TeacherCodeGenerator(EdvanzDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<string> GenerateUniqueCodeAsync()
    {
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            string code = GenerateCryptographicCode();

            bool exists = await _context.Teachers
                .AsNoTracking()
                .AnyAsync(t => t.TeacherCode == code);

            if (!exists)
                return code;
        }

        throw new InvalidOperationException(
            $"Failed to generate a unique teacher code after {MaxAttempts} attempts. " +
            "This indicates an extremely unlikely collision scenario or a database issue.");
    }

    /// <summary>
    /// Generates a cryptographically random 8-digit numeric string.
    /// Range: 10000000 to 99999999 (always exactly 8 digits).
    /// Uses RandomNumberGenerator which is cryptographically secure per AAM-NFR-03.
    /// </summary>
    private static string GenerateCryptographicCode()
    {
        // Generate a random integer in the range [10000000, 99999999]
        int code = RandomNumberGenerator.GetInt32(10_000_000, 100_000_000);
        return code.ToString("D8");
    }
}