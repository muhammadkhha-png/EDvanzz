using Edvanz.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Interfaces
{
    public interface IStudentTeacherLinkRepo:IGenericRepo<StudentTeacherLink,(long,long)>
    {
        public Task<(long studentAccountId, List<long> teacherIds)> GetSudentAccountLinkedTeacherIdsByUserId(long userId);
    }
}
