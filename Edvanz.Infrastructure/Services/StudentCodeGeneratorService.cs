using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// Generates sequential student codes for a teacher's account.
/// REQ-STU-007: Format is [Letter][Number].
/// AAM-FR-04.2: Supports both English (A-Z) and Arabic (أ-ي) letter sequences
///              based on the teacher's StudentCodeLanguage configuration.
/// 
/// English: A1 → A2 → ... → A999 → B1 → ... → Z999 (26 × 999 = 25,974 codes)
/// Arabic:  أ1 → أ2 → ... → أ999 → ب1 → ... → ي999 (28 × 999 = 27,972 codes)
/// 
/// Sequence per teacher, always advances forward (no gap filling per design decision).
/// If the highest existing code is B14 / ب14, the next generated code is B15 / ب15.
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

    /// <summary>
    /// English letter sequence: A through Z (26 letters).
    /// REQ-STU-007: A1 → Z999.
    /// </summary>
    private static readonly char[] EnglishLetters =
    {
        'A','B','C','D','E','F','G','H','I','J','K','L','M',
        'N','O','P','Q','R','S','T','U','V','W','X','Y','Z'
    };

    /// <summary>
    /// Arabic letter sequence: the 28 Arabic alphabet letters in standard order.
    /// AAM-FR-04.2: When StudentCodeLanguage == Arabic, codes use Arabic letter prefixes.
    /// أ1 → ب1 → ت1 → ... → ي999.
    /// 
    /// Using the isolated form of each letter since the code prefix is a standalone character,
    /// not connected to adjacent Arabic text. This matches what Egyptian teachers expect.
    /// </summary>
    private static readonly char[] ArabicLetters =
    {
        'أ', // Alef
        'ب', // Ba
        'ت', // Ta
        'ث', // Tha
        'ج', // Jim
        'ح', // Ha
        'خ', // Kha
        'د', // Dal
        'ذ', // Dhal
        'ر', // Ra
        'ز', // Zay
        'س', // Sin
        'ش', // Shin
        'ص', // Sad
        'ض', // Dad
        'ط', // Ta (emphatic)
        'ظ', // Dha (emphatic)
        'ع', // Ain
        'غ', // Ghain
        'ف', // Fa
        'ق', // Qaf
        'ك', // Kaf
        'ل', // Lam
        'م', // Mim
        'ن', // Nun
        'ه', // Ha
        'و', // Waw
        'ي'  // Ya
    };

    public StudentCodeGeneratorService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<string> GenerateNextCodeAsync(long teacherId, GenerationLanguage language = GenerationLanguage.English)
    {
        var letters = GetLetterSequence(language);

        var highestCode = await _unitOfWork.Students.GetHighestStudentCodeAsync(teacherId);

        if (string.IsNullOrEmpty(highestCode))
        {
            // No existing codes — start at first letter + 1
            return $"{letters[0]}1";
        }

        // Try to parse using the requested language's letter set first
        var parsed = ParseCode(highestCode, letters);

        if (parsed is null)
        {
            // Highest code doesn't match auto-generated pattern for this language
            // (could be a manual code, or from a different language era)
            // Start at first letter + 1 — the uniqueness check at save time will handle collisions
            return $"{letters[0]}1";
        }

        return ComputeNextCode(parsed.Value.letterIndex, parsed.Value.number, letters);
    }

    /// <summary>
    /// Returns the letter sequence array for the given language.
    /// </summary>
    private static char[] GetLetterSequence(GenerationLanguage language)
    {
        return language == GenerationLanguage.Arabic ? ArabicLetters : EnglishLetters;
    }

    /// <summary>
    /// Parses a code string into its letter index and number components
    /// using the provided letter sequence.
    /// Returns null if the code doesn't match the [Letter][Number] pattern.
    /// 
    /// For English: first char must be A-Z, rest must be digits.
    /// For Arabic: first char must be one of the 28 Arabic letters, rest must be digits.
    /// </summary>
    private static (int letterIndex, int number)? ParseCode(string code, char[] letters)
    {
        if (code.Length < 2)
            return null;

        char firstChar = code[0];

        // Find the letter in the sequence
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
    /// Uses the provided letter sequence to support both English and Arabic.
    /// 
    /// Examples (English): A1 → A2, A999 → B1, Z999 → throws
    /// Examples (Arabic):  أ1 → أ2, أ999 → ب1, ي999 → throws
    /// </summary>
    private static string ComputeNextCode(int letterIndex, int number, char[] letters)
    {
        if (number < 999)
        {
            // Increment the number within the same letter
            return $"{letters[letterIndex]}{number + 1}";
        }

        // Number reached 999 — move to the next letter
        int nextLetterIndex = letterIndex + 1;

        if (nextLetterIndex < letters.Length)
        {
            return $"{letters[nextLetterIndex]}1";
        }

        // Last letter + 999 reached — code space exhausted
        string languageName = letters.Length == 28 ? "Arabic" : "English";
        int totalCodes = letters.Length * 999;
        throw new InvalidOperationException(
            $"Student code space exhausted ({letters[^1]}999 reached). " +
            $"The teacher has exceeded the maximum of {totalCodes:N0} auto-generated {languageName} student codes. " +
            "Consider using manual code entry for additional students.");
    }
}