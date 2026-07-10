using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Interfaces
{

    public interface IAssitantRepo : IGenericRepo<Assistant, long>
    {
        public Task<(IReadOnlyList<Assistant>, int)> GetListAssistantsPerTeacher(long? teacherId, bool? isAcitve, string? fullName, string? username, bool? isAssignedToTeacher, AssistantSortBy? sortby, SortDirection? sortDirection, int page, int pageSize);
        public Task<Assistant?> GetAssistantWithPermissionsAsync(long id);
        public Task<Assistant?> GetAssistantWithUserIdAsync(long id);

        /// <summary>
        /// Returns the <c>UserId</c> of every assistant currently linked to the given tutor
        /// account, used by the module-revocation fan-out (BR-ADM-010): when a super-admin
        /// removes a module from a tutor, every assistant under that tutor must have their
        /// cached auth snapshot invalidated and their <c>SecurityStamp</c> bumped so they
        /// lose access on their next request.
        ///
        /// SCOPE:
        ///   Includes deactivated and suspended assistants — they may still hold active
        ///   tokens, and we want to revoke those tokens on the next use regardless of
        ///   the assistant's current account status. Excludes soft-deleted rows
        ///   (<c>DeletedAt != null</c>) because their underlying user records are
        ///   typically already revoked.
        ///
        /// PERFORMANCE:
        ///   AsNoTracking single-column projection on an indexed FK — cheap.
        ///   A tutor typically has 1-10 assistants; this never returns more than a
        ///   small handful of ids.
        /// </summary>
        Task<IReadOnlyList<long>> GetUserIdsByTeacherAccountIdAsync(long teacherId);

        /// <summary>
        /// Counts the assistants owned by a tutor account (excluding soft-deleted rows).
        /// Used to enforce the free-tier assistant quota for unsubscribed teachers.
        /// </summary>
        Task<int> CountByTeacherAccountIdAsync(long teacherId);

    }
}
