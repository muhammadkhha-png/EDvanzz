using System.Text;
using Edvanz.Domain.Resources;
using Microsoft.Extensions.Localization;

namespace Edvanz.Application.Extensions
{
    /// <summary>
    /// Resolves the localized display name/description for a Module or Permission at the
    /// presentation/response layer, per the current request's Accept-Language.
    ///
    /// Reuses the existing <see cref="IStringLocalizer{Messages}"/> mechanism — same resx files
    /// (Messages.en.resx / Messages.ar.resx), no parallel localization system. The underlying
    /// stable identifiers (<c>Module.Name</c>, <c>Permission.Name</c>, and the numeric
    /// <c>Permission.Id</c> everything actually matches on for authorization/writes) are never
    /// touched — this only affects what a DTO puts in its display-text fields.
    ///
    /// KEY CONVENTION:
    ///   Module_{ModuleKey}
    ///   Permission_{ModuleKey}_{PermissionKey}
    ///   PermissionDescription_{ModuleKey}_{PermissionKey}
    /// where {ModuleKey}/{PermissionKey} are the seeded Name with every non-alphanumeric
    /// character stripped (resx data names can't contain spaces/hyphens). E.g. module
    /// "Event-Based Payment" + permission "View" → key "Permission_EventBasedPayment_View".
    /// Permission names repeat across modules ("View", "Edit", ...), so the module qualifier is
    /// required to avoid collisions — do not shorten this key shape.
    ///
    /// FALLBACK: if a resx entry is missing for a given key (new module/permission added without
    /// a matching translation yet), the raw seeded Name / stored Description is returned instead
    /// of the key itself, so the API never leaks an unresolved resx key to a client.
    /// </summary>
    public static class PermissionLocalizationExtensions
    {
        private static string SanitizeKey(string name)
        {
            var builder = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                    builder.Append(c);
            }
            return builder.ToString();
        }

        /// <summary>Localized module display name, falling back to <paramref name="moduleName"/>.</summary>
        public static string GetLocalizedModuleName(this IStringLocalizer<Messages> localizer, string moduleName)
        {
            var localized = localizer[$"Module_{SanitizeKey(moduleName)}"];
            return localized.ResourceNotFound ? moduleName : localized.Value;
        }

        /// <summary>Localized permission display name, falling back to <paramref name="permissionName"/>.</summary>
        public static string GetLocalizedPermissionName(
            this IStringLocalizer<Messages> localizer, string moduleName, string permissionName)
        {
            var key = $"Permission_{SanitizeKey(moduleName)}_{SanitizeKey(permissionName)}";
            var localized = localizer[key];
            return localized.ResourceNotFound ? permissionName : localized.Value;
        }

        /// <summary>
        /// Localized permission description, falling back to <paramref name="storedDescription"/>
        /// (the raw <c>Permission.Description</c> column) and finally to null if neither exists.
        /// </summary>
        public static string? GetLocalizedPermissionDescription(
            this IStringLocalizer<Messages> localizer,
            string moduleName,
            string permissionName,
            string? storedDescription)
        {
            var key = $"PermissionDescription_{SanitizeKey(moduleName)}_{SanitizeKey(permissionName)}";
            var localized = localizer[key];
            return localized.ResourceNotFound ? storedDescription : localized.Value;
        }
    }
}
