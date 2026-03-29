using Edvanz.Domain.Interfaces;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// Generates sequential student codes for a teacher's account.
/// REQ-STU-007: Format is [Letter][Number] — A1, A2 ... A999, B1 ... Z999.
/// 
/// Sequence per teacher, always advances forward (no gap filling per design decision).
/// If the highest existing code is B14, the next generated code is B15.
/// 
/// Code space: A1 → Z999 = 26 letters × 999 numbers = 25,974 unique codes.
/// This comfortably exceeds the max student capacity of 3,000+ per teacher.
/// 
/// CONCURRENCY SAFETY:
/// The composite unique index IX_TeacherStudents_TeacherId_StudentCode prevents
/// duplicate codes at the database level. If two concurrent requests generate
/// the same code, the second SaveChangesAsync will throw a unique constraint
/// violation, and the service layer handles the retry.
/// </summary>
public class StudentCodeGeneratorService : IStudentCodeGenerator
{
    private readonly IUnitOfWork _unitOfWork;

    public StudentCodeGeneratorService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<string> GenerateNextCodeAsync(long teacherId)
    {
        var highestCode = await _unitOfWork.Students.GetHighestStudentCodeAsync(teacherId);

        if (string.IsNullOrEmpty(highestCode))
        {
            // No existing codes — start at A1
            return "A1";
        }

        // Parse the highest code to determine the next one
        var parsed = ParseCode(highestCode);

        if (parsed is null)
        {
            // Highest code doesn't match auto-generated pattern (could be manual code)
            // Start at A1 — the uniqueness check at save time will handle collisions
            return "A1";
        }

        return ComputeNextCode(parsed.Value.letter, parsed.Value.number);
    }

    /// <summary>
    /// Parses a code string into its letter and number components.
    /// Returns null if the code doesn't match the [A-Z][0-9]+ pattern.
    /// </summary>
    private static (char letter, int number)? ParseCode(string code)
    {
        if (code.Length < 2)
            return null;

        char letter = code[0];

        // Must be uppercase A-Z
        if (letter < 'A' || letter > 'Z')
            return null;

        string numberPart = code[1..];

        if (!int.TryParse(numberPart, out int number) || number < 1)
            return null;

        return (letter, number);
    }

    /// <summary>
    /// Computes the next sequential code after the given letter+number.
    /// A1 → A2, A999 → B1, Z999 → throws (code space exhausted).
    /// </summary>
    private static string ComputeNextCode(char letter, int number)
    {
        if (number < 999)
        {
            // Increment the number within the same letter
            return $"{letter}{number + 1}";
        }

        // Number reached 999 — move to the next letter
        if (letter < 'Z')
        {
            char nextLetter = (char)(letter + 1);
            return $"{nextLetter}1";
        }

        // Z999 reached — code space exhausted (25,974 codes used)
        throw new InvalidOperationException(
            "Student code space exhausted (Z999 reached). " +
            "The teacher has exceeded the maximum of 25,974 auto-generated student codes. " +
            "Consider using manual code entry for additional students.");
    }
}