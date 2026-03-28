namespace Edvanz.Application.Dtos.Teacher;

/// <summary>
/// Input DTO for updating a teacher's own profile.
/// 
/// Updatable fields:
///   - FullName (AAM-FR-03.4: Arabic or English)
///   - LanguagePreference (AAM-FR-02.3: changeable from settings, controls UI language)
///   - SubjectIds (AAM-FR-03.5: update selected subjects)
///   - CustomSubject (AAM-FR-03.5: free-text for unlisted subjects)
///   - StudentCapacityPackageId (AAM-FR-04.1: teacher selects their capacity tier,
///     which auto-updates StudentCapacity and determines subscription fees)
/// 
/// NOT updatable via this endpoint (by design):
///   - TeacherCode (AAM-BR-05: permanent and immutable)
///   - AccountStatus (REQ-ADM-017: super admin only)
///   - Email, Username, Password, PhoneNumber (managed by User module)
/// 
/// Note: LanguagePreference controls the system UI language (Arabic/English).
/// This is independent from StudentCodeLanguage and SessionNameLanguage on
/// TeacherConfiguration, which control how auto-generated codes and session
/// names are produced (e.g., system in English but codes in Arabic).
/// </summary>
public class UpdateTeacherProfileDto
{
    /// <summary>
    /// Teacher's full name. Supports Arabic and English input.
    /// AAM-FR-03.4: The teacher shall provide their full name with the option to enter it in Arabic or English.
    /// </summary>
    public string FullName { get; set; } = null!;

    /// <summary>
    /// Preferred UI language: "en" or "ar".
    /// AAM-FR-02.3: Changeable at any time from account settings.
    /// Controls the system display language only. Does NOT affect code/session name generation language.
    /// </summary>
    public string LanguagePreference { get; set; } = null!;

    /// <summary>
    /// Selected student capacity package tier Id.
    /// AAM-FR-04.1: Determines subscription tier and auto-updates StudentCapacity.
    /// Nullable — if not provided, the current package is unchanged.
    /// </summary>
    public long? StudentCapacityPackageId { get; set; }

    /// <summary>
    /// Updated list of ministry-defined subject Ids.
    /// Replaces all existing subject associations.
    /// At least one SubjectId or a CustomSubject is required.
    /// </summary>
    public List<long> SubjectIds { get; set; } = new();

    /// <summary>
    /// Optional free-text subject name for subjects not in the ministry list.
    /// Set to null or empty to clear.
    /// </summary>
    public string? CustomSubject { get; set; }
}