using Edvanz.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Interfaces
{
    public interface IStudentTeacherLinkRepo:IGenericRepo<StudentTeacherLink,(long,long)>
    {
        public Task<(long studentAccountId, List<long> teacherIds)> GetSudentAccountLinkedTeacherIdsByUserId(long userId);

        /// <summary>
        /// Batch count of ACTIVE student-account links per teacher (students who connected their
        /// account) for a set of teachers — one GROUP BY for the admin teacher list. Teachers with
        /// no active links are absent from the result.
        /// </summary>
        Task<Dictionary<long, int>> GetActiveLinkedCountsAsync(IReadOnlyCollection<long> teacherIds);
    }
}
