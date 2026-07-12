using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.TeacherLinks;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.Extensions.Localization;
using System.Net;

namespace Edvanz.Application.Services;

/// <summary>
/// Implements the teacher-side half of the link request/approval flow.
/// All data access goes through IUnitOfWork.Users named methods (no raw
/// predicates), results follow the Result pattern, and student notifications
/// run post-commit as best-effort side effects via IStudentLinkNotifier.
/// </summary>
public class TeacherStudentLinkService : ITeacherStudentLinkService
{
    private const int MaxPageSize = 100;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IStudentLinkNotifier _linkNotifier;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;

    public TeacherStudentLinkService(
        IUnitOfWork unitOfWork,
        IStudentLinkNotifier linkNotifier,
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _linkNotifier = linkNotifier;
        _localizer = localizer;
    }

    /// <inheritdoc />
    public async Task<Result<TeacherCodeDto>> GetMyTeacherCodeAsync(long teacherId)
    {
        var teacher = await _unitOfWork.Users.GetTeacherByIdAsync(teacherId);
        if (teacher is null)
            return Result<TeacherCodeDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        return Result<TeacherCodeDto>.Success(
            new TeacherCodeDto { TeacherCode = teacher.TeacherCode }, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<TeacherLinkRequestListItemDto>>>> GetPendingLinkRequestsAsync(
        long teacherId, int page, int pageSize)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);

        var (rows, totalCount) = await _unitOfWork.Users
            .GetPendingLinkRequestsForTeacherPagedAsync(teacherId, page, pageSize);

        // Suggested roster matches: one batch query for all typed codes on this
        // page, plus one batch query for their claim state — never per-row.
        var codes = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.RequestedStudentCode))
            .Select(r => r.RequestedStudentCode!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var matchesByCode = new Dictionary<string, TeacherStudent>(StringComparer.OrdinalIgnoreCase);
        var claimedIds = new HashSet<long>();

        if (codes.Count > 0)
        {
            var rosterMatches = await _unitOfWork.Users.GetActiveTeacherStudentsByCodesAsync(teacherId, codes);
            foreach (var ts in rosterMatches)
                matchesByCode[ts.StudentCode] = ts;

            if (rosterMatches.Count > 0)
            {
                var claimed = await _unitOfWork.Users.GetActivelyLinkedTeacherStudentIdsAsync(
                    rosterMatches.Select(ts => ts.Id).ToList());
                claimedIds = claimed.ToHashSet();
            }
        }

        var items = rows.Select(r =>
        {
            RosterStudentSuggestionDto? suggestion = null;
            if (!string.IsNullOrWhiteSpace(r.RequestedStudentCode) &&
                matchesByCode.TryGetValue(r.RequestedStudentCode!, out var ts))
            {
                suggestion = new RosterStudentSuggestionDto
                {
                    TeacherStudentId = ts.Id,
                    StudentName = ts.StudentName,
                    StudentCode = ts.StudentCode,
                    IsAlreadyLinked = claimedIds.Contains(ts.Id)
                };
            }

            return new TeacherLinkRequestListItemDto
            {
                LinkId = r.LinkId,
                RequestedStudentName = r.RequestedStudentName,
                RequestedStudentCode = r.RequestedStudentCode,
                RequestedAt = r.RequestedAt,
                StudentAccountCode = r.StudentAccountCode,
                StudentFullName = r.StudentFullName,
                StudentPhoneNumber = r.StudentPhoneNumber,
                SuggestedMatch = suggestion
            };
        }).ToList();

        var response = new PaginatedResponse<List<TeacherLinkRequestListItemDto>>
        {
            data = items,
            page = page,
            pageSize = pageSize,
            totalCount = totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
        };

        return Result<PaginatedResponse<List<TeacherLinkRequestListItemDto>>>.Success(response, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<LinkedStudentListItemDto>> AcceptLinkRequestAsync(
        long teacherId, long linkId, long actingUserId, AcceptLinkRequestDto dto)
    {
        var link = await _unitOfWork.Users.GetStudentTeacherLinkByIdForTeacherAsync(linkId, teacherId);
        if (link is null)
            return Result<LinkedStudentListItemDto>.Failure(_localizer, "LinkRequestNotFound", HttpStatusCode.NotFound);

        if (link.LinkStatus != LinkStatus.Pending)
            return Result<LinkedStudentListItemDto>.Failure(_localizer, "LinkRequestAlreadyResolved", HttpStatusCode.Conflict);

        // ── Accept CONNECTS the account (Active); binding it to a student record is
        // a SEPARATE step (BindStudentLinkAsync). TeacherStudentId is an optional
        // "Accept & link" shortcut — when omitted the link is accepted UNBOUND
        // (connected but Not linked, no data access) and can be linked later. ──
        TeacherStudent? rosterStudent = null;
        if (dto.TeacherStudentId.HasValue)
        {
            rosterStudent = await _unitOfWork.Users.GetActiveTeacherStudentByIdAsync(teacherId, dto.TeacherStudentId.Value);
            if (rosterStudent is null)
                return Result<LinkedStudentListItemDto>.Failure(_localizer, "RosterStudentNotFound", HttpStatusCode.NotFound);

            // One student account per student record.
            bool alreadyClaimed = await _unitOfWork.Users.IsTeacherStudentActivelyLinkedAsync(rosterStudent.Id);
            if (alreadyClaimed)
                return Result<LinkedStudentListItemDto>.Failure(_localizer, "RosterStudentAlreadyClaimed", HttpStatusCode.Conflict);
        }

        var now = DateTime.UtcNow;
        link.TeacherStudentId = rosterStudent?.Id;   // null = accepted but Not linked
        link.LinkStatus = LinkStatus.Active;
        link.LinkedAt = now;
        link.RespondedAt = now;
        link.RespondedByUserId = actingUserId;

        await _unitOfWork.Users.UpdateStudentTeacherLinkAsync(link);
        await _unitOfWork.SaveChangesAsync();

        // ── Post-commit, best-effort: tell the student the request was accepted ──
        try
        {
            await _linkNotifier.NotifyRequestResolvedAsync(link.StudentUserId, teacherId, accepted: true);
        }
        catch { /* notification failure must not fail the accept */ }

        var item = await BuildLinkedStudentItemAsync(link, rosterStudent);
        return Result<LinkedStudentListItemDto>.Success(item, _localizer, "LinkRequestAccepted");
    }

    /// <inheritdoc />
    public async Task<Result<LinkedStudentListItemDto>> BindStudentLinkAsync(
        long teacherId, long linkId, long actingUserId, BindStudentLinkDto dto)
    {
        var link = await _unitOfWork.Users.GetStudentTeacherLinkByIdForTeacherAsync(linkId, teacherId);
        if (link is null)
            return Result<LinkedStudentListItemDto>.Failure(_localizer, "LinkNotFound", HttpStatusCode.NotFound);

        // Only an ACCEPTED (Active) connection can be linked — Pending must be
        // accepted first, terminal states are not linkable.
        if (link.LinkStatus != LinkStatus.Active)
            return Result<LinkedStudentListItemDto>.Failure(_localizer, "LinkNotActive", HttpStatusCode.Conflict);

        // ── Resolve the target student record: explicit id wins, else by code ──
        TeacherStudent? rosterStudent;
        if (dto.TeacherStudentId.HasValue)
        {
            rosterStudent = await _unitOfWork.Users.GetActiveTeacherStudentByIdAsync(teacherId, dto.TeacherStudentId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(dto.StudentCode))
        {
            rosterStudent = await _unitOfWork.Users.GetActiveTeacherStudentByCodeAsync(
                teacherId, dto.StudentCode.Trim().ToUpperInvariant());
        }
        else
        {
            return Result<LinkedStudentListItemDto>.Failure(_localizer, "BindTargetRequired", HttpStatusCode.BadRequest);
        }

        if (rosterStudent is null)
            return Result<LinkedStudentListItemDto>.Failure(_localizer, "RosterStudentNotFound", HttpStatusCode.NotFound);

        // Already pointed at this record → idempotent success (no notification).
        if (link.TeacherStudentId == rosterStudent.Id)
        {
            var same = await BuildLinkedStudentItemAsync(link, rosterStudent);
            return Result<LinkedStudentListItemDto>.Success(same, _localizer, "LinkBound");
        }

        // One student account per record. This link holds a different (or no) record,
        // so any Active holder of the target is necessarily a DIFFERENT account.
        bool alreadyClaimed = await _unitOfWork.Users.IsTeacherStudentActivelyLinkedAsync(rosterStudent.Id);
        if (alreadyClaimed)
            return Result<LinkedStudentListItemDto>.Failure(_localizer, "RosterStudentAlreadyClaimed", HttpStatusCode.Conflict);

        link.TeacherStudentId = rosterStudent.Id;   // first bind, or re-point ("Change")
        await _unitOfWork.Users.UpdateStudentTeacherLinkAsync(link);
        await _unitOfWork.SaveChangesAsync();

        // ── Post-commit, best-effort: the student just gained access ──
        try
        {
            await _linkNotifier.NotifyLinkBindingChangedAsync(link.StudentUserId, teacherId, linked: true);
        }
        catch { /* notification failure must not fail the bind */ }

        var item = await BuildLinkedStudentItemAsync(link, rosterStudent);
        return Result<LinkedStudentListItemDto>.Success(item, _localizer, "LinkBound");
    }

    /// <inheritdoc />
    public async Task<Result<LinkedStudentListItemDto>> UnbindStudentLinkAsync(
        long teacherId, long linkId, long actingUserId)
    {
        var link = await _unitOfWork.Users.GetStudentTeacherLinkByIdForTeacherAsync(linkId, teacherId);
        if (link is null)
            return Result<LinkedStudentListItemDto>.Failure(_localizer, "LinkNotFound", HttpStatusCode.NotFound);

        if (link.LinkStatus != LinkStatus.Active)
            return Result<LinkedStudentListItemDto>.Failure(_localizer, "LinkNotActive", HttpStatusCode.Conflict);

        bool wasLinked = link.TeacherStudentId.HasValue;
        link.TeacherStudentId = null;               // stays Active (connected), loses access
        await _unitOfWork.Users.UpdateStudentTeacherLinkAsync(link);
        await _unitOfWork.SaveChangesAsync();

        if (wasLinked)
        {
            // ── Post-commit, best-effort: the student just lost access ──
            try
            {
                await _linkNotifier.NotifyLinkBindingChangedAsync(link.StudentUserId, teacherId, linked: false);
            }
            catch { /* notification failure must not fail the unbind */ }
        }

        var item = await BuildLinkedStudentItemAsync(link, rosterStudent: null);
        return Result<LinkedStudentListItemDto>.Success(item, _localizer, "LinkUnbound");
    }

    /// <inheritdoc />
    public async Task<Result<bool>> RejectLinkRequestAsync(long teacherId, long linkId, long actingUserId)
    {
        var link = await _unitOfWork.Users.GetStudentTeacherLinkByIdForTeacherAsync(linkId, teacherId);
        if (link is null)
            return Result<bool>.Failure(_localizer, "LinkRequestNotFound", HttpStatusCode.NotFound);

        if (link.LinkStatus != LinkStatus.Pending)
            return Result<bool>.Failure(_localizer, "LinkRequestAlreadyResolved", HttpStatusCode.Conflict);

        link.LinkStatus = LinkStatus.Rejected;
        link.RespondedAt = DateTime.UtcNow;
        link.RespondedByUserId = actingUserId;

        await _unitOfWork.Users.UpdateStudentTeacherLinkAsync(link);
        await _unitOfWork.SaveChangesAsync();

        // ── Post-commit, best-effort: tell the student the request was rejected ──
        try
        {
            await _linkNotifier.NotifyRequestResolvedAsync(link.StudentUserId, teacherId, accepted: false);
        }
        catch { /* notification failure must not fail the reject */ }

        return Result<bool>.Success(true, _localizer, "LinkRequestRejected");
    }

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<LinkedStudentListItemDto>>>> GetLinkedStudentsAsync(
        long teacherId, int page, int pageSize)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);

        var (rows, totalCount) = await _unitOfWork.Users
            .GetActiveLinkedStudentsForTeacherPagedAsync(teacherId, page, pageSize);

        var items = rows.Select(r => new LinkedStudentListItemDto
        {
            LinkId = r.LinkId,
            LinkedAt = r.LinkedAt,
            StudentAccountCode = r.StudentAccountCode,
            StudentFullName = r.StudentFullName,
            StudentPhoneNumber = r.StudentPhoneNumber,
            TeacherStudentId = r.TeacherStudentId,
            RosterStudentName = r.RosterStudentName,
            RosterStudentCode = r.RosterStudentCode,
            IsLinked = r.TeacherStudentId.HasValue
        }).ToList();

        var response = new PaginatedResponse<List<LinkedStudentListItemDto>>
        {
            data = items,
            page = page,
            pageSize = pageSize,
            totalCount = totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
        };

        return Result<PaginatedResponse<List<LinkedStudentListItemDto>>>.Success(response, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<RemoveLinkedStudentsResultDto>> RemoveLinkedStudentsAsync(
        long teacherId, long actingUserId, RemoveLinkedStudentsDto dto)
    {
        var requestedIds = dto.LinkIds.Distinct().ToList();
        if (requestedIds.Count == 0)
            return Result<RemoveLinkedStudentsResultDto>.Failure(_localizer, "NoLinksSelected", HttpStatusCode.BadRequest);

        var links = await _unitOfWork.Users.GetActiveLinksByIdsForTeacherAsync(teacherId, requestedIds);
        if (links.Count == 0)
            return Result<RemoveLinkedStudentsResultDto>.Failure(_localizer, "LinkNotFound", HttpStatusCode.NotFound);

        var now = DateTime.UtcNow;
        foreach (var link in links)
        {
            link.LinkStatus = LinkStatus.RemovedByTeacher;
            link.UnlinkedAt = now;
            link.RemovedByUserId = actingUserId;
            await _unitOfWork.Users.UpdateStudentTeacherLinkAsync(link);
        }

        await _unitOfWork.SaveChangesAsync();

        // ── Post-commit, best-effort: tell each affected student ──
        foreach (var link in links)
        {
            try
            {
                await _linkNotifier.NotifyRemovedByTeacherAsync(link.StudentUserId, teacherId);
            }
            catch { /* notification failure must not fail the removal */ }
        }

        var removedIds = links.Select(l => l.Id).ToHashSet();
        var result = new RemoveLinkedStudentsResultDto
        {
            RemovedCount = links.Count,
            SkippedLinkIds = requestedIds.Where(id => !removedIds.Contains(id)).ToList()
        };

        return Result<RemoveLinkedStudentsResultDto>.Success(result, _localizer, "LinkedStudentsRemoved");
    }

    // ══════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════

    private static (int page, int pageSize) NormalizePaging(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;
        return (page, pageSize);
    }

    /// <summary>
    /// Builds the linked-student list item returned by a successful accept so the
    /// teacher UI can insert the row without refetching the whole list.
    /// </summary>
    private async Task<LinkedStudentListItemDto> BuildLinkedStudentItemAsync(
        StudentTeacherLink link, TeacherStudent? rosterStudent)
    {
        var studentUser = await _unitOfWork.Users.GetStudentUserByIdAsync(link.StudentUserId);
        User? accountUser = studentUser is null
            ? null
            : await _unitOfWork.Users.GetUserByIdAsync(studentUser.UserId);

        return new LinkedStudentListItemDto
        {
            LinkId = link.Id,
            LinkedAt = link.LinkedAt,
            StudentAccountCode = studentUser?.StudentAccountCode ?? string.Empty,
            StudentFullName = accountUser?.FullName ?? string.Empty,
            StudentPhoneNumber = accountUser?.PhoneNumber,
            TeacherStudentId = rosterStudent?.Id,
            RosterStudentName = rosterStudent?.StudentName,
            RosterStudentCode = rosterStudent?.StudentCode,
            IsLinked = rosterStudent is not null
        };
    }
}