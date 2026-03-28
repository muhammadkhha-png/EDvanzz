using Edvanz.Domain.Entities.ShareProp;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Represents a teaching subject defined by the Egyptian Ministry of Education.
/// AAM-FR-03.5: Available subjects include ministry-defined subjects plus a free-text custom field.
/// This table holds the predefined subjects; custom subjects are stored on the Teacher entity.
/// Super-admin-managed lookup table.
/// </summary>
public class Subject : BaseEntity
{
    /// <summary>
    /// Subject name in English (e.g., "Mathematics", "Science", "Arabic").
    /// </summary>
    public string NameEn { get; set; } = null!;

    /// <summary>
    /// Subject name in Arabic (e.g., "رياضيات", "علوم", "عربي").
    /// </summary>
    public string NameAr { get; set; } = null!;

    /// <summary>
    /// Soft-delete flag. Inactive subjects are hidden from registration but preserved for historical data.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Controls the display order in dropdown lists. Lower values appear first.
    /// </summary>
    public int DisplayOrder { get; set; }

    // Navigation
    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
}