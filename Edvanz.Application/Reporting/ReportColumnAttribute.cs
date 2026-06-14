namespace Edvanz.Application.Reporting;

/// <summary>
/// Marks a DTO property as a report column, defining its order and the localization key
/// for its header. Format-neutral sibling to the Excel-specific [ExcelColumn]; consumed by
/// the generic PDF export service to discover columns and their order via reflection.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class ReportColumnAttribute : Attribute
{
    /// <summary>Localization resource key for the column header (resolved by the caller).</summary>
    public string Key { get; }

    /// <summary>Ordinal controlling column order.</summary>
    public int Order { get; }

    public ReportColumnAttribute(string key, int order)
    {
        Key = key;
        Order = order;
    }
}