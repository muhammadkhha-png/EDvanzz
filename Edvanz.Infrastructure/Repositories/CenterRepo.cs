using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories;

/// <summary>
/// Center tenancy repository — see <see cref="ICenterRepo"/>. Soft-deleted centers/teachers are
/// excluded by the entities' global query filters (DeletedAt == null); the counts below therefore
/// naturally ignore deleted rows.
/// </summary>
public class CenterRepo : GenericRepo<Center, long>, ICenterRepo
{
    public CenterRepo(EdvanzDbContext context) : base(context)
    {
    }

    public Task<Center?> GetCenterByUserIdAsync(long userId) =>
        _context.Set<Center>().FirstOrDefaultAsync(c => c.UserId == userId && c.DeletedAt == null);

    public Task<CenterAssistant?> GetCenterAssistantByUserIdAsync(long userId) =>
        _context.Set<CenterAssistant>()
            .Include(a => a.Center)
            .FirstOrDefaultAsync(a => a.UserId == userId && a.DeletedAt == null);

    public Task<Center?> GetCenterByIdAsync(long centerId) =>
        _context.Set<Center>().FirstOrDefaultAsync(c => c.Id == centerId && c.DeletedAt == null);

    public async Task<IReadOnlyList<CenterAssistant>> GetCenterAssistantsByCenterAsync(long centerId) =>
        await _context.Set<CenterAssistant>()
            .Include(a => a.User)
            .Where(a => a.CenterId == centerId && a.DeletedAt == null)
            .OrderByDescending(a => a.Id)
            .ToListAsync();

    public Task<CenterAssistant?> GetCenterAssistantByIdAsync(long centerAssistantId) =>
        _context.Set<CenterAssistant>()
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == centerAssistantId && a.DeletedAt == null);

    public Task<bool> IsTeacherInCenterAsync(long centerId, long teacherId) =>
        _context.Set<Teacher>().AnyAsync(t => t.Id == teacherId && t.CenterId == centerId);

    public Task<bool> IsActiveTeacherInCenterAsync(long centerId, long teacherId) =>
        _context.Set<Teacher>().AnyAsync(t => t.Id == teacherId
                                           && t.CenterId == centerId
                                           && t.AccountStatus != AccountStatus.Inactive);

    public Task<bool> ExistsByCenterCodeAsync(string centerCode) =>
        _context.Set<Center>().AnyAsync(c => c.CenterCode == centerCode);

    public async Task<IReadOnlyList<long>> GetTeacherIdsByCenterAsync(long centerId) =>
        await _context.Set<Teacher>()
            .AsNoTracking()
            .Where(t => t.CenterId == centerId)
            .Select(t => t.Id)
            .ToListAsync();

    public async Task<IReadOnlyList<Teacher>> GetTeachersByCenterAsync(long centerId) =>
        await _context.Set<Teacher>()
            .Include(t => t.User)
            .Where(t => t.CenterId == centerId)
            .OrderBy(t => t.Id)
            .ToListAsync();

    public Task<int> CountActiveTeachersByPlanAsync(long centerId, SubscriptionPlanType plan) =>
        _context.Set<Teacher>()
            .CountAsync(t => t.CenterId == centerId
                          && t.CenterPlanType == plan
                          && t.AccountStatus != AccountStatus.Inactive);

    public Task<int> CountCenterStudentsByPlanAsync(long centerId, SubscriptionPlanType plan) =>
        (from ts in _context.Set<TeacherStudent>()
         join t in _context.Set<Teacher>() on ts.TeacherId equals t.Id
         where t.CenterId == centerId && t.CenterPlanType == plan
         select ts.Id).CountAsync();

    public Task<int> CountCenterStudentsTotalAsync(long centerId) =>
        (from ts in _context.Set<TeacherStudent>()
         join t in _context.Set<Teacher>() on ts.TeacherId equals t.Id
         where t.CenterId == centerId
         select ts.Id).CountAsync();

    public async Task<Dictionary<long, int>> GetStudentCountsByCenterTeachersAsync(long centerId) =>
        await (from ts in _context.Set<TeacherStudent>()
               join t in _context.Set<Teacher>() on ts.TeacherId equals t.Id
               where t.CenterId == centerId
               group ts by ts.TeacherId into g
               select new { TeacherId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TeacherId, x => x.Count);

    public async Task<IReadOnlyList<string>> GetAllStudentCodesForCenterAsync(long centerId) =>
        await (from ts in _context.Set<TeacherStudent>()
               join t in _context.Set<Teacher>() on ts.TeacherId equals t.Id
               where t.CenterId == centerId
               select ts.StudentCode)
            .ToListAsync();

    public Task<CenterSubscription?> GetCurrentCenterSubscriptionAsync(long centerId) =>
        _context.Set<CenterSubscription>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CenterId == centerId && s.IsCurrent);

    public Task<CenterSubscription?> GetCurrentCenterSubscriptionForUpdateAsync(long centerId)
    {
        const string sql =
            "SELECT * FROM CenterSubscriptions WITH (UPDLOCK, HOLDLOCK) " +
            "WHERE CenterId = {0} AND IsCurrent = 1";
        return _context.Set<CenterSubscription>()
            .FromSqlRaw(sql, centerId)
            .FirstOrDefaultAsync();
    }

    public async Task FlipCurrentAndInsertNewCenterSubscriptionAsync(
        CenterSubscription? previousCurrent, CenterSubscription newSubscription)
    {
        if (previousCurrent != null)
            previousCurrent.IsCurrent = false;

        newSubscription.IsCurrent = true;
        await _context.Set<CenterSubscription>().AddAsync(newSubscription);
    }

    public Task<CenterSubscriptionRequest?> GetPendingRequestByCenterAsync(long centerId) =>
        _context.Set<CenterSubscriptionRequest>()
            .FirstOrDefaultAsync(r => r.CenterId == centerId
                                   && r.Status == SubscriptionRequestStatus.Pending);

    public Task<CenterSubscriptionRequest?> GetCenterSubscriptionRequestByIdAsync(long requestId) =>
        _context.Set<CenterSubscriptionRequest>()
            .FirstOrDefaultAsync(r => r.Id == requestId);

    public async Task<IReadOnlyList<CenterSubscriptionRequest>> GetPendingCenterSubscriptionRequestsAsync() =>
        await _context.Set<CenterSubscriptionRequest>()
            .AsNoTracking()
            .Where(r => r.Status == SubscriptionRequestStatus.Pending)
            .OrderBy(r => r.RequestedAt)
            .ToListAsync();

    // ── Teacher independence requests (center-teacher asks to leave the center) ──

    public Task<TeacherIndependenceRequest?> GetPendingIndependenceRequestByTeacherAsync(long teacherId) =>
        _context.Set<TeacherIndependenceRequest>()
            .FirstOrDefaultAsync(r => r.TeacherId == teacherId
                                   && r.Status == SubscriptionRequestStatus.Pending);

    public Task<TeacherIndependenceRequest?> GetLatestIndependenceRequestByTeacherAsync(long teacherId) =>
        _context.Set<TeacherIndependenceRequest>()
            .AsNoTracking()
            .Where(r => r.TeacherId == teacherId)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync();

    public Task<TeacherIndependenceRequest?> GetIndependenceRequestByIdAsync(long requestId) =>
        _context.Set<TeacherIndependenceRequest>()
            .FirstOrDefaultAsync(r => r.Id == requestId);

    public async Task<IReadOnlyList<TeacherIndependenceRequest>> GetPendingIndependenceRequestsAsync() =>
        await _context.Set<TeacherIndependenceRequest>()
            .AsNoTracking()
            .Include(r => r.Teacher).ThenInclude(t => t.User)
            .Include(r => r.Center)
            .Where(r => r.Status == SubscriptionRequestStatus.Pending)
            .OrderBy(r => r.RequestedAt)
            .ToListAsync();

    public async Task<IReadOnlyList<CenterStudentCodeMatch>> ResolveStudentsByCodeAcrossCenterAsync(long centerId, string code) =>
        await (from ts in _context.Set<TeacherStudent>()
               join t in _context.Set<Teacher>() on ts.TeacherId equals t.Id
               join u in _context.Set<User>() on t.UserId equals u.Id
               join s in _context.Set<Session>() on ts.SessionId equals s.Id into sj
               from s in sj.DefaultIfEmpty()
               where t.CenterId == centerId && ts.StudentCode == code
               orderby u.FullName
               select new CenterStudentCodeMatch
               {
                   TeacherId = t.Id,
                   TeacherName = u.FullName,
                   TeacherCode = t.TeacherCode,
                   TeacherStudentId = ts.Id,
                   StudentName = ts.StudentName,
                   StudentCode = ts.StudentCode,
                   StudentPhoneNumber = ts.StudentPhoneNumber,
                   SessionId = ts.SessionId,
                   SessionName = s != null ? s.SessionName : null
               })
            .AsNoTracking()
            .ToListAsync();
}
