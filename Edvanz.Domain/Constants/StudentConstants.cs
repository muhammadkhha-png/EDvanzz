namespace Edvanz.Domain.Constants;

/// <summary>
/// Constants for the Student Module (Module 1). Single-sources the module name
/// string and permission names so they are compile-time checked across the
/// [ModulePermission] attributes, the permission seeder, and any module-active
/// gate. Same pattern as <see cref="VideoConstants"/>. Change a value in exactly
/// one place: here.
///
/// The values MUST match the rows seeded by DbInitializer.SeedPermissionsAsync
/// under module "Student".
/// </summary>
public static class StudentConstants
{
    /// <summary>Module name as registered in the Modules seed table and emitted in JWT module claims.</summary>
    public const string ModuleName = "Student";

    public const string PermissionViewList = "ViewList";
    public const string PermissionViewProfile = "ViewProfile";
    public const string PermissionAdd = "Add";
    public const string PermissionEdit = "Edit";
    public const string PermissionDelete = "Delete";
    public const string PermissionImport = "Import";
    public const string PermissionExportReports = "ExportReports";
    public const string PermissionViewBarcodes = "ViewBarcodes";
}