using Edvanz.Domain.Entities;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Infrastructure.Repositories
{
    public class StudentTeacherLinkRepo : GenericRepo<StudentTeacherLink, (long, long)>, IStudentTeacherLinkRepo
    {
        public StudentTeacherLinkRepo(EdvanzDbContext context) : base(context)
        {
        }

        public async Task<(long studentAccountId, List<long> teacherIds)> GetSudentAccountLinkedTeacherIdsByUserId(long userId)
        {
            var links = await _context.StudentTeacherLinks
           .Where(link => link.StudentUserId == userId)
           .ToListAsync();

            if (!links.Any())
                return (0, new List<long>());

            var teacherIds = links.Select(link => link.TeacherId).ToList();
            var studentAccountId = links.First().StudentUserId;

            return (studentAccountId, teacherIds);
        }

       
    }
}
