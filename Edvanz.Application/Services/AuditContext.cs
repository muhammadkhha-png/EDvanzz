using Edvanz.Application.IservicesContract;
using Microsoft.AspNetCore.Http;

namespace Edvanz.Application.Services
{
    /// <summary>
    /// HttpContext.Items-backed implementation of <see cref="IAuditContext"/>.
    /// Mirrors CurrentUserService's use of IHttpContextAccessor.
    /// </summary>
    public class AuditContext : IAuditContext
    {
        // Private to this type. The attribute reads through IAuditContext, never the
        // raw key, so the key never leaks across the layer boundary.
        private const string ItemsKey = "__Audit.AffectedRecord";

        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc />
        public void SetAffectedRecord(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return;

            var ctx = _httpContextAccessor.HttpContext;
            if (ctx is not null)
                ctx.Items[ItemsKey] = label.Trim();
        }

        /// <inheritdoc />
        public string? AffectedRecord
        {
            get
            {
                var items = _httpContextAccessor.HttpContext?.Items;
                if (items is not null && items.TryGetValue(ItemsKey, out var value))
                    return value as string;

                return null;
            }
        }
    }
}