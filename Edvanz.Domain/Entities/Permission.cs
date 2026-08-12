using Edvanz.Domain.Entities.ShareProp;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Edvanz.Domain.Entities
{
    public class Permission:BaseEntity
    {
        public string Name { get; set; }
        [ForeignKey(nameof(module))]
        public long  ModuleId { get; set; }
        public Module module { get; set; }
        public bool IsRestricted { get; set; }

        /// <summary>
        /// Default/fallback description of what this permission grants. Backfilled via a
        /// data-only migration for existing rows (DbInitializer does not run in Production).
        /// The API response layer prefers a localized "PermissionDescription_{Module}_{Name}"
        /// resx entry over this raw value when one exists — this column is the stable,
        /// language-agnostic fallback, not the primary source of the displayed text.
        /// </summary>
        public string? Description { get; set; }
        public virtual ICollection<UsersPermission> Permissions { get; set; } = new List<UsersPermission>();
        public virtual ICollection<TemplatePermisions> Profiles { get; set; } = new List<TemplatePermisions>();

    }
}
