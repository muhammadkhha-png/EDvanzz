using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// Generates sequential session names for a teacher's account.
/// REQ-SES-002: Auto-generation pattern: Session A1, Session A2 … Session B1, Session B2 …
/// REQ-SES-004: Language determined by TeacherConfiguration.SessionNameLanguage.
/// 
/// English: Session A1 → Session A2 → … → Session A999 → Session B1 → … → Session Z999
/// Arabic:  حصة أ1 → حصة أ2 → … → حصة أ999 → حصة ب1 → … → حصة ي999
/// 
/// Mirrors the StudentCodeGeneratorService pattern exactly.
/// Sequence per teacher, always advances forward (no gap filling).
/// 
/// CONCURRENCY SAFETY:
/// The composite unique index IX_Sessions_TeacherId_SessionName prevents
/// duplicate names at the database level. If two concurrent requests generate
/// the same name, the second SaveChangesAsync will throw a unique constraint
/// violation, and the service layer handles the retry.
/// </summary>
public class SessionNameGeneratorService : ISessionNameGenerator
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// English prefix for auto-generated session names.
    /// </summary>
    private const string EnglishPrefix = "Session ";

    /// <summary>
    /// Arabic prefix for auto-generated session names.
    /// "حصة" = "Session" in Egyptian Arabic.
    /// </summary>
    private const string ArabicPrefix = "حصة ";

    /// <summary>
    /// English letter sequence: A through Z (26 letters).
    /// </summary>
    private static readonly char[] EnglishLetters =
    {
        'A','B','C','D','E','F','G','H','I','J','K','L','M',
        'N','O','P','Q','R','S','T','U','V','W','X','Y','Z'
    };

    /// <summary>
    /// Arabic letter sequence: the 28 Arabic alphabet letters in standard order.
    /// Using isolated form since the letter appears standalone in the name.
    /// </summary>
    private static readonly char[] ArabicLetters =
    {
        'أ', 'ب', 'ت', 'ث', 'ج', 'ح', 'خ', 'د', 'ذ', 'ر',
        'ز', 'س', 'ش', 'ص', 'ض', 'ط', 'ظ', 'ع', 'غ', 'ف',
        'ق', 'ك', 'ل', 'م', 'ن', 'ه', 'و', 'ي'
    };

    public SessionNameGeneratorService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<string> GenerateNextNameAsync(long teacherId, GenerationLanguage language = GenerationLanguage.English)
    {
        var letters = GetLetterSequence(language);
        var prefix = GetPrefix(language);

        var highestName = await _unitOfWork.SessionsRepo.GetHighestSessionNameAsync(teacherId);

        if (string.IsNullOrEmpty(highestName))
        {
            // No existing sessions — start at first letter + 1
            return $"{prefix}{letters[0]}1";
        }

        // Try to extract the code part from the highest name
        var codePart = ExtractCodePart(highestName, language);

        if (codePart is null)
        {
            // Highest name doesn't match auto-generated pattern
            // Start at first letter + 1
            return $"{prefix}{letters[0]}1";
        }

        var parsed = ParseCode(codePart, letters);

        if (parsed is null)
        {
            return $"{prefix}{letters[0]}1";
        }

        var nextCode = ComputeNextCode(parsed.Value.letterIndex, parsed.Value.number, letters, language);
        return $"{prefix}{nextCode}";
    }

    /// <summary>
    /// Returns the letter sequence array for the given language.
    /// </summary>
    private static char[] GetLetterSequence(GenerationLanguage language)
    {
        return language == GenerationLanguage.Arabic ? ArabicLetters : EnglishLetters;
    }

    /// <summary>
    /// Returns the name prefix for the given language.
    /// </summary>
    private static string GetPrefix(GenerationLanguage language)
    {
        return language == GenerationLanguage.Arabic ? ArabicPrefix : EnglishPrefix;
    }

    /// <summary>
    /// Extracts the code part (e.g., "A1", "ب15") from an auto-generated session name.
    /// Returns null if the name doesn't match the expected pattern.
    /// </summary>
    private static string? ExtractCodePart(string sessionName, GenerationLanguage language)
    {
        // Try the requested language's prefix first
        var prefix = GetPrefix(language);
        if (sessionName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && sessionName.Length > prefix.Length)
        {
            return sessionName[prefix.Length..];
        }

        // Also try the other language's prefix for cross-language scenarios
        var otherPrefix = language == GenerationLanguage.Arabic ? EnglishPrefix : ArabicPrefix;
        if (sessionName.StartsWith(otherPrefix, StringComparison.OrdinalIgnoreCase) && sessionName.Length > otherPrefix.Length)
        {
            return sessionName[otherPrefix.Length..];
        }

        return null;
    }

    /// <summary>
    /// Parses a code string into its letter index and number components.
    /// Returns null if the code doesn't match the [Letter][Number] pattern.
    /// </summary>
    private static (int letterIndex, int number)? ParseCode(string code, char[] letters)
    {
        if (code.Length < 2)
            return null;

        char firstChar = code[0];
        int letterIndex = Array.IndexOf(letters, firstChar);
        if (letterIndex < 0)
            return null;

        string numberPart = code[1..];
        if (!int.TryParse(numberPart, out int number) || number < 1)
            return null;

        return (letterIndex, number);
    }

    /// <summary>
    /// Computes the next sequential code after the given letter index + number.
    /// Examples (English): A1 → A2, A999 → B1, Z999 → throws
    /// </summary>
    private static string ComputeNextCode(int letterIndex, int number, char[] letters, GenerationLanguage language)
    {
        if (number < 999)
        {
            return $"{letters[letterIndex]}{number + 1}";
        }

        int nextLetterIndex = letterIndex + 1;
        if (nextLetterIndex < letters.Length)
        {
            return $"{letters[nextLetterIndex]}1";
        }

        string languageName = language == GenerationLanguage.Arabic ? "Arabic" : "English";
        int totalNames = letters.Length * 999;
        throw new InvalidOperationException(
            $"Session name space exhausted ({letters[^1]}999 reached). " +
            $"The teacher has exceeded the maximum of {totalNames:N0} auto-generated {languageName} session names. " +
            "Consider using manual name entry for additional sessions.");
    }
}