namespace Edvanz.Application.Dtos.Subscription;

/// <summary>
/// Output DTO for the admin tutor-module read endpoints:
///   GET /api/admin/tutor-modules/catalogue   → every platform module
///   GET /api/admin/tutor-modules/{teacherId}  → modules granted to one tutor
///
/// Flat projection of a <c>Module</c> (Models table) row. Serializes to
/// { id, name }, matching the Angular admin <c>ModuleInfo</c> interface consumed
/// by modules-panel.component.ts.
/// </summary>
public class ModuleInfoDto
{
    /// <summary>The Module.Id (Models table primary key).</summary>
    public long Id { get; set; }

    /// <summary>The module display name as seeded in Models.Name (e.g. "Payment").</summary>
    public string Name { get; set; } = null!;
}