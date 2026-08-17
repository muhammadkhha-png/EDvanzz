using System.Security.Cryptography;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// Generates unique 8-digit numeric center codes using a cryptographic RNG, with a DB uniqueness
/// check against the Centers table. Mirrors <see cref="TeacherCodeGenerator"/>.
/// </summary>
public class CenterCodeGenerator : ICenterCodeGenerator
{
    private readonly EdvanzDbContext _context;
    private const int MaxAttempts = 10;

    public CenterCodeGenerator(EdvanzDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<string> GenerateUniqueCodeAsync()
    {
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            string code = GenerateCryptographicCode();

            bool exists = await _context.Centers
                .AsNoTracking()
                .AnyAsync(c => c.CenterCode == code);

            if (!exists)
                return code;
        }

        throw new InvalidOperationException(
            $"Failed to generate a unique center code after {MaxAttempts} attempts.");
    }

    private static string GenerateCryptographicCode()
    {
        int code = RandomNumberGenerator.GetInt32(10_000_000, 100_000_000);
        return code.ToString("D8");
    }
}
