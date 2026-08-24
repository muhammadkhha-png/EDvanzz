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
        /// <summary>
        /// Returns a platform-wide, paginated list of assistants for the Admin Portal.
        /// Unlike <see cref="GetListAssistantsPerTeacher"/>, this is NOT tenant-scoped —
        /// intended for SuperAdmin callers only (enforced at the controller/service level).
        /// </summary>
        /// <param name="teacherId">Optional — restrict results to a single teacher's assistants.</param>
        /// <param name="search">Optional — matches assistant full name OR phone number (contains, case-insensitive).</param>
        Task<(IReadOnlyList<Assistant>, int)> GetAllAssistantsAsync(
            long? teacherId,
            string? search,
            AssistantSortBy? sortBy,
            SortDirection? sortDirection,
            int page,
            int pageSize);

        /// <summary>
        /// Per-teacher assistant stats for one PAGE of the SuperAdmin teacher list
        /// (one GROUP BY, mirrors ITeacherStudentRepo.GetActiveStudentCountsAsync):
        /// row count plus the max LastActivityAt / LastLoginAt across the teacher's
        /// assistants — feeds AssistantCount and the team last-activity rollup on the
        /// Activity Monitor. Deliberately spans the SAME population as
        /// <see cref="GetAllAssistantsAsync"/> (no DeletedAt filter) so the collapsed
        /// count always equals the rows shown when the teacher is expanded. Teachers
        /// with no assistants are absent from the dictionary — callers default to
        /// (0, null, null).
        /// </summary>
        Task<Dictionary<long, (int Count, DateTime? MaxLastActivityAt, DateTime? MaxLastLoginAt)>>
            GetAssistantActivityStatsAsync(IReadOnlyCollection<long> teacherIds);

    }
}
