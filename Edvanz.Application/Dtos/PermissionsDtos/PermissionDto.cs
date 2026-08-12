using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.PermissionsDtos
{
    public class PermissionDto
    {
        public long permissionId { get; set; }
        public string permissionName { get; set; }
        public bool isRestricted { get; set; }

        /// <summary>
        /// Localized display name of the owning module (per the request's Accept-Language),
        /// falling back to the raw <c>Module.Name</c> when no localized entry exists.
        /// New field — added so callers that only had a qualified "Module.Permission" string to
        /// parse (see <c>permissionName</c> note below) have an explicit way to get the module.
        /// </summary>
        public string? moduleName { get; set; }

        /// <summary>
        /// Localized description of what this permission grants (per the request's
        /// Accept-Language), falling back to the raw <c>Permission.Description</c> column value,
        /// and null if neither exists.
        /// </summary>
        public string? description { get; set; }

    }
}
