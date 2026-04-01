using Edvanz.Application.Dtos;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Edvanz.Infrastructure.Repositories;

/// <summary>
/// Extended repository for the Student Module (Module 1: Students).
/// Centralizes all domain-specific query logic for TeacherStudent records.
/// 
/// ARCHITECTURAL NOTE (same rationale as UserRepo):
/// All expression-based queries are encapsulated here so the Application layer
/// never builds raw predicates. If a query changes, you edit it HERE —
/// not in every service that uses it.
/// 
/// Inherits from GenericRepo&lt;TeacherStudent, long&gt; for basic CRUD.
/// </summary>
public class TeacherStudentRepo : GenericRepo<TeacherStudent, long>, ITeacherStudentRepo
{
    public TeacherStudentRepo(EdvanzDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<TeacherStudent?> GetActiveByCodeAndTeacherAsync(long teacherId, string studentCode)
    {
        // Global query filter already excludes IsDeleted == true
        return await _context.TeacherStudents
            .FirstOrDefaultAsync(ts => ts.TeacherId == teacherId
                                    && ts.StudentCode.ToLower() == studentCode.ToLower());
    }
    
    /// <inheritdoc />
    public async Task<TeacherStudent?> GetActiveByBarcodeAndTeacherAsync(long teacherId, string barcodeData)
    {
        // Barcode data = student code per REQ-STU-047
        return await _context.TeacherStudents
            .FirstOrDefaultAsync(ts => ts.TeacherId == teacherId
                                    && ts.Barcode == barcodeData);
    }
    // ══════════════════════════════════════════════
    // SINGLE RECORD QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<TeacherStudent?> GetActiveByIdAndTeacherAsync(long studentId, long teacherId)
    {
        // Global query filter already excludes IsDeleted == true
        return await _context.TeacherStudents
            .FirstOrDefaultAsync(ts => ts.Id == studentId && ts.TeacherId == teacherId);
    }

    /// <inheritdoc />
    public async Task<TeacherStudent?> GetByIdAndTeacherIgnoreFiltersAsync(long studentId, long teacherId)
    {
        // IgnoreQueryFilters to access soft-deleted records in the recycle bin
        return await _context.TeacherStudents
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ts => ts.Id == studentId && ts.TeacherId == teacherId);
    }

    // ══════════════════════════════════════════════
    // UNIQUENESS & EXISTENCE CHECKS
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<bool> StudentCodeExistsAsync(long teacherId, string studentCode)
    {
        // Global filter already excludes soft-deleted — active records only
        return await _context.TeacherStudents
            .AnyAsync(ts => ts.TeacherId == teacherId
                         && ts.StudentCode == studentCode);
    }

    /// <inheritdoc />
    public async Task<bool> StudentCodeExistsExcludingAsync(long teacherId, string studentCode, long excludeStudentId)
    {
        return await _context.TeacherStudents
            .AnyAsync(ts => ts.TeacherId == teacherId
                         && ts.StudentCode == studentCode
                         && ts.Id != excludeStudentId);
    }

    // ══════════════════════════════════════════════
    // CODE GENERATION SUPPORT
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<string?> GetHighestStudentCodeAsync(long teacherId)
    {
        // IgnoreQueryFilters: consider ALL codes including soft-deleted
        // to avoid generating a code that collides with a recycle bin record.
        // Only consider codes matching the auto-generated pattern [A-Z][0-9]+
        // so manually entered codes (e.g., "CUSTOM01") don't interfere with sequencing.
        //
        // We order by letter first (descending), then by the numeric suffix (descending)
        // to find the highest sequential code. EF Core translates this to SQL.
        return await _context.TeacherStudents
            .IgnoreQueryFilters()
            .Where(ts => ts.TeacherId == teacherId
                      && ts.StudentCode.Length >= 2
                      && ts.StudentCode.Length <= 5) // A1 (2 chars) to Z999 (4 chars) — max pattern length
            .OrderByDescending(ts => ts.StudentCode.Substring(0, 1)) // Letter descending (Z → A)
            .ThenByDescending(ts => ts.StudentCode.Length) // Longer numeric suffix = higher number
            .ThenByDescending(ts => ts.StudentCode) // Final lexicographic tie-break
            .Select(ts => ts.StudentCode)
            .FirstOrDefaultAsync();
    }

    // ══════════════════════════════════════════════
    // COUNT QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<int> CountActiveStudentsAsync(long teacherId)
    {
        // Global filter already excludes soft-deleted
        return await _context.TeacherStudents
            .CountAsync(ts => ts.TeacherId == teacherId);
    }

    /// <inheritdoc />
    public async Task<int> CountRecycleBinStudentsAsync(long teacherId)
    {
        return await _context.TeacherStudents
            .IgnoreQueryFilters()
            .CountAsync(ts => ts.TeacherId == teacherId && ts.IsDeleted);
    }

    // ══════════════════════════════════════════════
    // LIST / SEARCH / FILTER QUERIES
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public IQueryable<TeacherStudent> BuildStudentListQuery(
        long teacherId,
        string? search = null,
        long? sessionId = null,
        bool missingStudentPhone = false,
        bool missingParentPhone = false,
        bool missingSession = false,
        StudentSortBy sortBy = StudentSortBy.DateAdded,
        SortDirection sortDirection = SortDirection.Desc)
    {
        // Start with active students for this teacher (global filter handles IsDeleted)
        var query = _context.TeacherStudents
            .Where(ts => ts.TeacherId == teacherId);

        // ── FILTERS (applied BEFORE sort per REQ-STU-SRT-005) ──

        // REQ-STU-036: Filter by assigned session
        if (sessionId.HasValue)
        {
            query = query.Where(ts => ts.SessionId == sessionId.Value);
        }

        // REQ-STU-036: Missing student phone number
        if (missingStudentPhone)
        {
            query = query.Where(ts => ts.StudentPhoneNumber == null || ts.StudentPhoneNumber == "");
        }

        // REQ-STU-036: Missing parent phone number
        if (missingParentPhone)
        {
            query = query.Where(ts => ts.ParentPhoneNumber == null || ts.ParentPhoneNumber == "");
        }

        // REQ-STU-036: Missing assigned session
        if (missingSession)
        {
            query = query.Where(ts => ts.SessionId == null);
        }

        // ── SEARCH (applied after filters) ──
        // REQ-STU-032: Search by name or code (partial match)
        // REQ-STU-034: Case-insensitive, supports Arabic and English
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(ts =>
                ts.StudentName.Contains(term) ||
                ts.StudentCode.Contains(term));
        }

        // ── SORT (applied AFTER filter per REQ-STU-SRT-005) ──
        query = sortBy switch
        {
            StudentSortBy.StudentName => sortDirection == SortDirection.Asc
                ? query.OrderBy(ts => ts.StudentName)
                : query.OrderByDescending(ts => ts.StudentName),

            StudentSortBy.StudentCode => sortDirection == SortDirection.Asc
                ? query.OrderBy(ts => ts.StudentCode)
                : query.OrderByDescending(ts => ts.StudentCode),

            // Default: DateAdded (CreateAt)
            // REQ-STU-SRT-002: Default sort is DateAdded descending (newest first)
            _ => sortDirection == SortDirection.Asc
                ? query.OrderBy(ts => ts.CreateAt)
                : query.OrderByDescending(ts => ts.CreateAt)
        };

        return query;
    }

    /// <inheritdoc />
    public IQueryable<TeacherStudent> BuildRecycleBinQuery(long teacherId)
    {
        // IgnoreQueryFilters to see soft-deleted records
        return _context.TeacherStudents
            .IgnoreQueryFilters()
            .Where(ts => ts.TeacherId == teacherId && ts.IsDeleted)
            .OrderByDescending(ts => ts.DeletedAt);
    }

    // ══════════════════════════════════════════════
    // BULK OPERATIONS
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<IReadOnlyList<TeacherStudent>> GetActiveByIdsAndTeacherAsync(long teacherId, IEnumerable<long> studentIds)
    {
        var idList = studentIds.ToList();
        // Global filter excludes soft-deleted
        return await _context.TeacherStudents
            .Where(ts => ts.TeacherId == teacherId && idList.Contains(ts.Id))
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TeacherStudent>> GetDeletedByIdsAndTeacherAsync(long teacherId, IEnumerable<long> studentIds)
    {
        var idList = studentIds.ToList();
        return await _context.TeacherStudents
            .IgnoreQueryFilters()
            .Where(ts => ts.TeacherId == teacherId && ts.IsDeleted && idList.Contains(ts.Id))
            .ToListAsync();
    }

    // ══════════════════════════════════════════════
    // PURGE (PERMANENT DELETE)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<IReadOnlyList<TeacherStudent>> GetExpiredRecycleBinRecordsAsync()
    {
        // REQ-STU-027: 10-day retention window from DeletedAt
        var cutoffDate = DateTime.UtcNow.AddDays(-10);

        return await _context.TeacherStudents
            .IgnoreQueryFilters()
            .Where(ts => ts.IsDeleted && ts.DeletedAt != null && ts.DeletedAt.Value < cutoffDate)
            .ToListAsync();
    }
}