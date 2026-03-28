using System.Security.Cryptography;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// Generates unique 10-character alphanumeric student account codes
/// using a cryptographic random number generator.
/// AAM-FR-05.3: Auto-generated upon successful student registration.
/// AAM-NFR-03: Cryptographically unique and collision-resistant.
/// 
/// Format: 10 uppercase letters and digits (e.g., "STU4X7B2KM").
/// This is distinct from the 8-digit numeric TeacherCode and from
/// the teacher-assigned StudentCode (TeacherStudent.StudentCode).
/// </summary>
public class StudentAccountCodeGenerator : IStudentAccountCodeGenerator
{
    private readonly EdvanzDbContext _context;

    /// <summary>
    /// Characters available for code generation: uppercase letters + digits.
    /// 36^10 ≈ 3.6 trillion possible codes — collision is statistically negligible.
    /// </summary>
    private const string AllowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>
    /// Length of the generated student account code.
    /// </summary>
    private const int CodeLength = 10;

    /// <summary>
    /// Maximum number of generation attempts before throwing to prevent infinite loops.
    /// </summary>
    private const int MaxAttempts = 10;

    public StudentAccountCodeGenerator(EdvanzDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<string> GenerateUniqueCodeAsync()
    {
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            string code = GenerateCryptographicCode();

            bool exists = await _context.StudentUsers
                .AsNoTracking()
                .AnyAsync(s => s.StudentAccountCode == code);

            if (!exists)
                return code;
        }

        throw new InvalidOperationException(
            $"Failed to generate a unique student account code after {MaxAttempts} attempts. " +
            "This indicates an extremely unlikely collision scenario or a database issue.");
    }

    /// <summary>
    /// Generates a cryptographically random 10-character alphanumeric string.
    /// Uses RandomNumberGenerator which is cryptographically secure per AAM-NFR-03.
    /// </summary>
    private static string GenerateCryptographicCode()
    {
        var codeChars = new char[CodeLength];

        for (int i = 0; i < CodeLength; i++)
        {
            int index = RandomNumberGenerator.GetInt32(0, AllowedChars.Length);
            codeChars[i] = AllowedChars[index];
        }

        return new string(codeChars);
    }
}