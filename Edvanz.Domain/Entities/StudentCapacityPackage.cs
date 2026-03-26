using Edvanz.Domain.Entities.ShareProp;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Defines the student capacity tiers available during teacher configuration.
/// AAM-FR-04.1: 7 predefined tiers from "Up to 300" through "3000+".
/// Super-admin-managed lookup table — tiers can be adjusted without code deployment.
/// </summary>
public class StudentCapacityPackage : BaseEntity
{
    /// <summary>
    /// Display name shown to the teacher (e.g., "Up to 300", "300 to 500", "3000+").
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Lower bound of the tier (inclusive). Zero for the first tier.
    /// </summary>
    public int MinStudents { get; set; }

    /// <summary>
    /// Upper bound of the tier (inclusive). Null represents unlimited (the "3000+" tier).
    /// </summary>
    public int? MaxStudents { get; set; }

    /// <summary>
    /// Soft-delete flag. Inactive packages are hidden from selection but preserved for existing references.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Controls the display order in the package selection UI. Lower values appear first.
    /// </summary>
    public int DisplayOrder { get; set; }

    // Navigation
    public ICollection<Teacher> Teachers { get; set; } = new List<Teacher>();
}