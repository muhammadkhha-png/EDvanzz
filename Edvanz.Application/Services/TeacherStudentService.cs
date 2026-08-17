using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.TeacherStudent;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
// STU-3: reuse the centralized Egyptian-mobile validator so roster phones follow the SAME
// rule enforced at sign-up (UserService.PhoneNumberValidator). Mirrors AuthService's usage.
using static Edvanz.Application.Services.UserService;

namespace Edvanz.Application.Services;

/// <summary>
/// Implements all Student Module (Module 1) operations.
/// Manages teacher-scoped student records: CRUD, search, filter, bulk import,
/// recycle bin, and student code generation.
/// 
/// All database access goes through IUnitOfWork.Students (ITeacherStudentRepo)
/// and IUnitOfWork.Users (IUserRepo for teacher validation) — no direct
/// GetRepository calls with raw expression predicates.
/// 
/// ARCHITECTURAL NOTE:
/// All query logic is encapsulated in ITeacherStudentRepo named methods.
/// If a query changes, you edit the repo method — not this service.
/// 
/// FIX GAP-1: Code generation now respects TeacherConfiguration.StudentCodeLanguage.
///            Passes the language to IStudentCodeGenerator.GenerateNextCodeAsync so
///            codes are generated with Arabic or English letter prefixes per AAM-FR-04.2.
/// 
/// FIX GAP-2: Bulk import now respects TeacherConfiguration.StudentCodeGenerationMode.
///            When mode is Manual, rows with blank codes are rejected (consistent with
///            single-entry behavior per REQ-STU-011.1).
/// </summary>
public class TeacherStudentService : ITeacherStudentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStudentCodeGenerator _codeGenerator;
    private readonly IPaymentService _paymentService;
    private readonly IAttendanceService _attendanceService;
    private readonly ISubscriptionGateService _subscriptionGate;
    /// <summary>
    /// Shared student teardown: unassign + END the student ACCOUNT link on soft delete, and
    /// clear the blocking video/online-exam/homework FKs before a permanent delete. Lives in
    /// its own service (not here) because <see cref="IPaymentService"/> — which this service
    /// already injects — needs the same teardown for the departure flow; a hook on
    /// ITeacherStudentService would have created a circular dependency.
    /// </summary>
    private readonly IStudentTeardownService _teardownService;
    private readonly ILogger<TeacherStudentService> _logger;
    private readonly IStringLocalizer<Domain.Resources.Messages> _localizer;

    /// <summary>
    /// Regex for manual student code format validation.
    /// REQ-STU-CODE-001: 1-10 alphanumeric characters (A-Z, a-z, 0-9 only).
    /// </summary>
    private static readonly Regex StudentCodeFormatRegex = new(@"^[A-Za-z0-9]{1,10}$", RegexOptions.Compiled);

    public TeacherStudentService(
        IUnitOfWork unitOfWork,
        IStudentCodeGenerator codeGenerator,
        IPaymentService paymentService,
        IAttendanceService attendanceService,
        ISubscriptionGateService subscriptionGate,
        IStudentTeardownService teardownService,
        ILogger<TeacherStudentService> logger,
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _codeGenerator = codeGenerator;
        _paymentService = paymentService;
        _subscriptionGate = subscriptionGate;
        _attendanceService = attendanceService;
        _teardownService = teardownService;
        _logger = logger;
        _localizer = localizer;
    }

    // ══════════════════════════════════════════════
    // SINGLE STUDENT CRUD
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    /// <summary>
    /// The center's REMAINING roster capacity for this teacher (min of the overall pool and the
    /// per-plan pool), or null when the teacher is not center-owned or the center has no active
    /// subscription (in which case only the per-teacher <c>StudentCapacity</c> applies). Center pools
    /// are enforced ON TOP of the per-teacher cap so a center can't exceed its purchased package.
    /// </summary>
    private async Task<int?> GetCenterRemainingStudentCapacityAsync(Domain.Entities.Teacher teacher)
    {
        if (teacher.CenterId is not long centerId) return null;
        var sub = await _unitOfWork.Centers.GetCurrentCenterSubscriptionAsync(centerId);
        if (sub is null) return null;

        var total = await _unitOfWork.Centers.CountCenterStudentsTotalAsync(centerId);
        var plan = teacher.CenterPlanType ?? Domain.Enums.SubscriptionPlanType.Full;
        var pool = await _unitOfWork.Centers.CountCenterStudentsByPlanAsync(centerId, plan);
        var poolCap = plan == Domain.Enums.SubscriptionPlanType.Managerial
            ? sub.StudentCapacityUnderManagerial
            : sub.StudentCapacityUnderFull;

        var remainingTotal = sub.StudentCapacityTotal - total;
        var remainingPool = poolCap - pool;
        return Math.Max(0, Math.Min(remainingTotal, remainingPool));
    }

    /// <summary>Returns the resx key when adding <paramref name="addCount"/> students would exceed the
    /// center pool, else null.</summary>
    private async Task<string?> CheckCenterStudentCapacityAsync(Domain.Entities.Teacher teacher, int addCount)
    {
        var remaining = await GetCenterRemainingStudentCapacityAsync(teacher);
        if (remaining is null) return null;
        return addCount > remaining.Value ? "CenterStudentCapacityExhausted" : null;
    }

    /// <summary>
    /// The effective student-code mode for a teacher: for a CENTER-owned teacher it's the teacher's
    /// own override, else the center default; for a standalone teacher it's the teacher's config.
    /// </summary>
    private async Task<GenerationMode> ResolveEffectiveCodeModeAsync(
        Domain.Entities.Teacher teacher, Domain.Entities.TeacherConfiguration? config)
    {
        if (teacher.CenterId is long centerId)
        {
            if (teacher.StudentCodeModeOverride.HasValue)
                return teacher.StudentCodeModeOverride.Value;
            var center = await _unitOfWork.Centers.GetCenterByIdAsync(centerId);
            return center?.StudentCodeGenerationMode ?? GenerationMode.Auto;
        }
        return config?.StudentCodeGenerationMode ?? GenerationMode.Auto;
    }

    public async Task<Result<TeacherStudentDto>> CreateStudentAsync(long teacherId, CreateTeacherStudentDto dto)
    {
        // 1. Validate teacher exists
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher is null)
            return Result<TeacherStudentDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // NOTE: a managerial subscription does NOT block building the roster — a managerial teacher
        // works normally EXCEPT that no student ACCOUNT may be linked to them. The managerial gate
        // therefore lives only on the student-account-link flow (request/accept/bind), not here.

        // 1b. Free-tier quota: unsubscribed teachers may keep at most the configured student count.
        if (!await _subscriptionGate.CanCreateAsync(
                teacherId, ModuleQuotaKeys.Students,
                () => _unitOfWork.Students.CountActiveStudentsAsync(teacherId)))
            return Result<TeacherStudentDto>.Failure(
                _localizer, SubscriptionConstants.Messages.SubscriptionRequired, HttpStatusCode.Forbidden);

        // 2. Validate student name is not empty (REQ-STU-014)
        if (string.IsNullOrWhiteSpace(dto.StudentName))
            return Result<TeacherStudentDto>.Failure(_localizer, "StudentNameRequired", HttpStatusCode.BadRequest);

        // 2b. STU-3: Validate phone-number format (Egyptian mobile) when provided — same rule as
        // sign-up. Both fields are optional, so only a non-blank value is checked. Blocks malformed
        // numbers from entering the roster and later feeding the WhatsApp/SMS parent-notifications.
        var phoneError = ValidatePhoneNumbers(dto.StudentPhoneNumber, dto.ParentPhoneNumber);
        if (phoneError is not null)
            return Result<TeacherStudentDto>.Failure(_localizer, phoneError, HttpStatusCode.BadRequest);

        // 3. Check student capacity limit (per-teacher)
        int activeCount = await _unitOfWork.Students.CountActiveStudentsAsync(teacherId);
        if (activeCount >= teacher.StudentCapacity)
            return Result<TeacherStudentDto>.Failure(_localizer, "StudentCapacityReached", HttpStatusCode.BadRequest);

        // 3b. Center student-pool limit (only for center-owned teachers with an active center subscription).
        var centerCapError = await CheckCenterStudentCapacityAsync(teacher, 1);
        if (centerCapError is not null)
            return Result<TeacherStudentDto>.Failure(_localizer, centerCapError, HttpStatusCode.Conflict);

        // 4. Resolve student code based on the EFFECTIVE mode (center default + per-teacher override for
        //    center-owned teachers; the teacher's own config for standalone teachers).
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        var effectiveCodeMode = await ResolveEffectiveCodeModeAsync(teacher, config);
        string studentCode;

        if (effectiveCodeMode == GenerationMode.Manual)
        {
            // REQ-STU-011.1: Manual mode — code is required
            if (string.IsNullOrWhiteSpace(dto.StudentCode))
                return Result<TeacherStudentDto>.Failure(_localizer, "StudentCodeRequiredManual", HttpStatusCode.BadRequest);

            // REQ-STU-CODE-001: Validate format
            var codeValidation = ValidateStudentCodeFormat(dto.StudentCode);
            if (codeValidation is not null)
                return Result<TeacherStudentDto>.Failure(codeValidation, HttpStatusCode.BadRequest);

            // REQ-STU-CODE-003: Normalize to uppercase
            studentCode = dto.StudentCode.Trim().ToUpperInvariant();

            // REQ-STU-010: Check uniqueness
            bool codeExists = await _unitOfWork.Students.StudentCodeExistsAsync(teacherId, studentCode);
            if (codeExists)
                return Result<TeacherStudentDto>.Failure(_localizer, "StudentCodeDuplicate", HttpStatusCode.Conflict);
        }
        else
        {
            // REQ-STU-008/009: Auto-generate mode. A manually entered code is not allowed here —
            // reject it with a clear message instead of silently discarding it.
            if (!string.IsNullOrWhiteSpace(dto.StudentCode))
                return Result<TeacherStudentDto>.Failure(_localizer, "StudentCodeNotAllowedAuto", HttpStatusCode.BadRequest);

            // FIX GAP-1: Pass the teacher's configured language preference (AAM-FR-04.2)
            var codeLanguage = config?.StudentCodeLanguage ?? GenerationLanguage.English;
            // Center-owned teacher → generate center-wide-unique (pass CenterId); standalone → per-teacher.
            studentCode = await _codeGenerator.GenerateNextCodeAsync(teacherId, codeLanguage, teacher.CenterId);
        }

        // 5. Generate hashed token (auto-generated, REQ-STU-004)
        string hashedToken = GenerateHashedToken();

        // 6. Generate barcode (REQ-STU-047: encodes the student code)
        string barcode = studentCode; // Barcode data = student code per REQ-STU-047

        // 6b. Resolve the optional session assignment. A null id → unassigned. STU-2: a non-null id
        // that does not resolve to a session owned by this teacher is now a clean 404 (previously it
        // was silently dropped and the student created unassigned, so the teacher wrongly believed
        // the assignment succeeded). This mirrors UpdateStudentAsync. Only a valid, owned session is
        // assigned — and it must go through the attendance/payment integration hooks below, otherwise
        // the student never appears on the attendance roster (the roster is driven by
        // StudentSessionAssignments, not TeacherStudent.SessionId).
        Session? assignSession = null;
        if (dto.SessionId.HasValue)
        {
            assignSession = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(dto.SessionId.Value, teacherId);
            if (assignSession is null)
                return Result<TeacherStudentDto>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);
        }

        // 7. Create the entity
        var student = new TeacherStudent
        {
            TeacherId = teacherId,
            StudentName = dto.StudentName.Trim(),
            StudentCode = studentCode,
            HashedToken = hashedToken,
            StudentPhoneNumber = NormalizePhone(dto.StudentPhoneNumber),
            ParentPhoneNumber = NormalizePhone(dto.ParentPhoneNumber),
            Barcode = barcode,
            SessionId = assignSession?.Id,
            IsDeleted = false,
            CreateAt = DateTime.UtcNow
        };

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _unitOfWork.Students.AddAsync(student);
            await _unitOfWork.SaveChangesAsync(); // materialize student.Id for the assignment hooks

            // When a valid session was supplied, create the assignment record + absence counter +
            // payment periods so the student surfaces on attendance and payment screens. Mirrors
            // SessionService.AssignStudentsAsync; the hooks do not commit — this method owns the tx.
            if (assignSession is not null)
            {
                await _attendanceService.OnStudentAssignedToSessionAsync(
                    teacherId, student.Id, assignSession.Id, assignSession.SessionName);
                await _paymentService.OnStudentAssignedToSessionAsync(
                    teacherId, student.Id, assignSession.Id, assignSession.SessionName, DateTime.UtcNow);
                await _unitOfWork.SaveChangesAsync();
            }

            await _unitOfWork.CommitAsync();
        }
        catch (DbUpdateException ex) when (ResolveUniqueViolationKey(ex) is { } messageKey)
        {
            await _unitOfWork.RollbackAsync();
            return Result<TeacherStudentDto>.Failure(_localizer, messageKey, HttpStatusCode.Conflict);
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }

        return Result<TeacherStudentDto>.Success(
            MapToDto(student,null), _localizer, "StudentCreatedSuccess", HttpStatusCode.Created);
    }

 
    /// <inheritdoc />
    public async Task<Result<TeacherStudentProfileDto>> GetStudentByIdAsync(long teacherId, long studentId)
    {
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(studentId, teacherId);
        if (student is null)
            return Result<TeacherStudentProfileDto>.Failure(_localizer, "StudentNotFound", HttpStatusCode.NotFound);

        // Profile screen: resolve the assigned-session card (name / occurrence / cost) in the
        // same call so the mobile profile renders without a second round-trip.
        SessionSummaryRow? session = student.SessionId.HasValue
            ? await _unitOfWork.SessionsRepo.GetSessionSummaryByIdAsync(teacherId, student.SessionId.Value)
            : null;
        var profile = MapToProfileDto(student, session);

        // LinkId enrichment (BUG-LINKID-01) — the profile screen's Unlink action needs a real
        // link id; previously this was always null here regardless of an existing Active link.
        profile.LinkId = await ResolveActiveLinkIdAsync(student.Id);

        return Result<TeacherStudentProfileDto>.Success(profile, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<TeacherStudentProfileDto>> GetStudentByIdForAdminAsync(long studentId)
    {
        var student = await _unitOfWork.Students.GetActiveByIdAsync(studentId);
        if (student is null)
            return Result<TeacherStudentProfileDto>.Failure(_localizer, "StudentNotFound", HttpStatusCode.NotFound);

        // Delegate to the teacher-scoped method now that TeacherId is known —
        // reuses the exact profile-assembly logic, zero duplication.
        var result = await GetStudentByIdAsync(student.TeacherId, studentId);

        // Admin-only enrichment: the mobile/teacher-scoped path above deliberately skips this
        // lookup (the caller there already knows their own name); the SuperAdmin profile
        // screen needs it, so it's added here as a single extra row fetch, not on the hot path.
        if (result.IsSuccess && result.Data is not null)
        {
            result.Data.TeacherName = await _unitOfWork.Users.GetTeacherDisplayNameAsync(student.TeacherId);
            var linkIds = await _unitOfWork.Users.GetActiveLinkIdsByTeacherStudentIdsAsync(new[] { studentId });
            result.Data.LinkId = linkIds.TryGetValue(studentId, out var linkId) ? linkId : null;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<Result<StudentCodeResolveDto>> ResolveByCodeAsync(long teacherId, string code)
    {
        // Canonical scan resolver. EXACT match on StudentCode (not the partial roster search the
        // old attendance manual-lookup used, which took .first and could resolve A1 -> A10). The
        // barcode/QR encodes the plain per-teacher StudentCode, so scanning + typing both land here.
        var trimmed = code?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return Result<StudentCodeResolveDto>.Failure(_localizer, "BarcodeRequired", HttpStatusCode.BadRequest);

        // Tenant-scoped exact lookup; the global soft-delete filter excludes deleted students, so a
        // reused-after-delete code resolves to the CURRENT active owner (permanence-per-code, §design).
        var student = await _unitOfWork.Students.GetActiveByCodeAndTeacherAsync(trimmed, teacherId);
        if (student is null)
            return Result<StudentCodeResolveDto>.Failure(_localizer, "StudentCodeNotFound", HttpStatusCode.NotFound);

        string? sessionName = null;
        if (student.SessionId.HasValue)
        {
            var names = await _unitOfWork.SessionsRepo.GetSessionNamesByIdsAsync(
                teacherId, new[] { student.SessionId.Value });
            names.TryGetValue(student.SessionId.Value, out sessionName);
        }

        return Result<StudentCodeResolveDto>.Success(new StudentCodeResolveDto
        {
            Id = student.Id,
            StudentName = student.StudentName,
            StudentCode = student.StudentCode,
            SessionId = student.SessionId,
            SessionName = sessionName
        }, _localizer, "Success");
    }

    /// <inheritdoc />
    public async Task<Result<TeacherStudentDto>> UpdateStudentForAdminAsync(
        long studentId, UpdateTeacherStudentDto dto)
    {
        var student = await _unitOfWork.Students.GetActiveByIdAsync(studentId);
        if (student is null)
            return Result<TeacherStudentDto>.Failure(_localizer, "StudentNotFound", HttpStatusCode.NotFound);

        // Delegate to the teacher-scoped method now that TeacherId is known —
        // reuses the exact validation/update logic (code rules, session
        // reassignment, RowVersion concurrency), zero duplication.
        return await UpdateStudentAsync(student.TeacherId, studentId, dto);
    }

    /// <inheritdoc />
    public async Task<Result<TeacherStudentDto>> UpdateStudentAsync(
        long teacherId, long studentId, UpdateTeacherStudentDto dto)
    {
        // 1. Find existing student
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(studentId, teacherId);
        if (student is null)
            return Result<TeacherStudentDto>.Failure(_localizer, "StudentNotFound", HttpStatusCode.NotFound);

        // 2. Validate name not empty
        if (string.IsNullOrWhiteSpace(dto.StudentName))
            return Result<TeacherStudentDto>.Failure(_localizer, "StudentNameRequired", HttpStatusCode.BadRequest);

        // 2b. STU-3: Validate phone-number format (Egyptian mobile) when provided — same rule as
        // create and sign-up. Both fields are optional, so only a non-blank value is checked.
        var phoneError = ValidatePhoneNumbers(dto.StudentPhoneNumber, dto.ParentPhoneNumber);
        if (phoneError is not null)
            return Result<TeacherStudentDto>.Failure(_localizer, phoneError, HttpStatusCode.BadRequest);

        // 3. Handle student code update. STU-1: behave consistently with create/bulk-import —
        //    - Auto mode: a manually supplied code is REJECTED (previously it was silently ignored,
        //      so the caller wrongly believed the code changed). A blank code is a no-op.
        //    - Manual mode: a supplied code is validated for format + uniqueness and applied; a blank
        //      code leaves the existing code unchanged (an edit that simply doesn't touch the code).
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        var teacherForMode = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        bool isManualMode = teacherForMode is not null
            && await ResolveEffectiveCodeModeAsync(teacherForMode, config) == GenerationMode.Manual;

        if (!isManualMode)
        {
            // Auto mode: reject any supplied code; leave the immutable auto-code untouched otherwise
            // (REQ-STU-048 spirit: auto-codes are immutable).
            if (!string.IsNullOrWhiteSpace(dto.StudentCode))
                return Result<TeacherStudentDto>.Failure(_localizer, "StudentCodeNotAllowedAuto", HttpStatusCode.BadRequest);
        }
        else if (!string.IsNullOrWhiteSpace(dto.StudentCode))
        {
            // Validate format
            var codeValidation = ValidateStudentCodeFormat(dto.StudentCode);
            if (codeValidation is not null)
                return Result<TeacherStudentDto>.Failure(codeValidation, HttpStatusCode.BadRequest);

            string normalizedCode = dto.StudentCode.Trim().ToUpperInvariant();

            // Check uniqueness excluding the current student
            bool codeExists = await _unitOfWork.Students.StudentCodeExistsExcludingAsync(
                teacherId, normalizedCode, studentId);
            if (codeExists)
                return Result<TeacherStudentDto>.Failure(_localizer, "StudentCodeDuplicate", HttpStatusCode.Conflict);

            student.StudentCode = normalizedCode;
            // Keep the denormalized Barcode column in lock-step with the code. Historically the
            // code edit updated StudentCode only, leaving Barcode pointing at the OLD code — so
            // the printed barcode (rendered from Barcode) no longer scanned. The renderer now
            // reads StudentCode, but we still sync the column so the exposed DTO.Barcode and the
            // payment-scan `Barcode == barcode` branch stay correct.
            student.Barcode = normalizedCode;
        }

        // Resolve the target session assignment. On an explicit single edit, a supplied id that
        // does not resolve to a session owned by this teacher is a clear error (unlike bulk import,
        // which neglects it). Null clears the assignment.
        long? previousSessionId = student.SessionId;
        Session? newSession = null;
        if (dto.SessionId.HasValue)
        {
            newSession = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(dto.SessionId.Value, teacherId);
            if (newSession is null)
                return Result<TeacherStudentDto>.Failure(_localizer, "SessionNotFound", HttpStatusCode.NotFound);
        }
        long? newSessionId = newSession?.Id;
        bool sessionChanged = previousSessionId != newSessionId;

        // 4. Update fields
        student.StudentName = dto.StudentName.Trim();
        student.StudentPhoneNumber = NormalizePhone(dto.StudentPhoneNumber);
        student.ParentPhoneNumber = NormalizePhone(dto.ParentPhoneNumber);
        student.SessionId = newSessionId;
        // REQ-STU-048: Barcode NEVER changes even if other fields are modified

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _unitOfWork.Students.UpdateAsync(student);
            await _unitOfWork.SaveChangesAsync();

            // Keep the StudentSessionAssignment (which drives attendance/payment screens) in sync
            // with the edited SessionId. OnStudentAssignedToSessionAsync deactivates any prior
            // active assignment before creating the new one.
            if (sessionChanged)
            {
                if (newSession is not null)
                {
                    // Attendance side is unchanged: create/reactivate the new session's assignment
                    // (OnStudentAssignedToSessionAsync deactivates any prior active assignment).
                    await _attendanceService.OnStudentAssignedToSessionAsync(
                        teacherId, student.Id, newSession.Id, newSession.SessionName);

                    if (previousSessionId is not null)
                    {
                        // GENUINE MOVE (A → B): carry the billing over in one shot (§7.4) — paid months
                        // stay in A as history, unpaid arrears + the current month MOVE to B (tagged
                        // MovedFrom*), a partial month is split (paid part stays, remainder billed in B),
                        // unpaid FUTURE months in A are cancelled, and B generates its own future months
                        // without re-billing any moved/already-paid month. Replaces the old
                        // unassign(A)+assign(B) pair, which STRANDED past arrears in A.
                        var prevSession = await _unitOfWork.SessionsRepo
                            .GetByIdAndTeacherAsync(previousSessionId.Value, teacherId);
                        await _paymentService.OnStudentMovedBetweenSessionsAsync(
                            teacherId, student.Id,
                            previousSessionId.Value, prevSession?.SessionName ?? string.Empty,
                            newSession.Id, newSession.SessionName, DateTime.UtcNow);
                    }
                    else
                    {
                        // FRESH ASSIGN (no previous session): generate the new session's periods.
                        await _paymentService.OnStudentAssignedToSessionAsync(
                            teacherId, student.Id, newSession.Id, newSession.SessionName, DateTime.UtcNow);
                    }
                }
                else
                {
                    await _attendanceService.OnStudentUnassignedFromSessionAsync(teacherId, student.Id);
                    await _paymentService.OnStudentUnassignedFromSessionAsync(teacherId, student.Id);
                }
                await _unitOfWork.SaveChangesAsync();
            }

            await _unitOfWork.CommitAsync();
        }
        catch (DbUpdateException ex) when (ResolveUniqueViolationKey(ex) is { } messageKey)
        {
            await _unitOfWork.RollbackAsync();
            return Result<TeacherStudentDto>.Failure(_localizer, messageKey, HttpStatusCode.Conflict);
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }

        var updatedDto = MapToDto(student, null);

        // Same LinkId enrichment as GetStudentByIdAsync (BUG-LINKID-01). Without this, editing
        // a student and re-rendering the row from the PUT response wipes out the LinkId the
        // list screen had shown, and the next Unbind click silently no-ops.
        updatedDto.LinkId = await ResolveActiveLinkIdAsync(student.Id);

        return Result<TeacherStudentDto>.Success(updatedDto, _localizer, "StudentUpdatedSuccess");
    }

    // ══════════════════════════════════════════════
    // STUDENT LIST (SEARCH + FILTER + PAGINATION)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<TeacherStudentDto>>>> GetStudentListAsync(
        long teacherId, StudentListRequest request)
    {
        // Validate teacher exists
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher is null)
            return Result<PaginatedResponse<List<TeacherStudentDto>>>.Failure(
                _localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // Build the filtered, sorted query via repo
        var query = _unitOfWork.Students.BuildStudentListQuery(
            teacherId,
            request.Search,
            request.SessionId,
            request.MissingStudentPhone,
            request.MissingParentPhone,
            request.MissingSession,
            request.SortBy,
            request.SortDirection);

        // Get total count AFTER filtering (for filtered count display)
        // Get total count AFTER filtering (for filtered count display)
        int totalCount = await _unitOfWork.Students.CountAsync(query);

        // Get the current page
        var students = await _unitOfWork.Students.GetPagedAsync(query, request.Page, request.PageSize);

        // Enrich the assigned-session badge (REQ-STU-036) without an N+1:
        // one lookup for the page's distinct session Ids. Mirrors the templateMap
        // pattern in AutomatedTriggerService.
        var sessionIds = students
            .Where(s => s.SessionId.HasValue)
            .Select(s => s.SessionId!.Value)
            .Distinct()
            .ToList();

        IReadOnlyDictionary<long, string> sessionNames = sessionIds.Count == 0
            ? new Dictionary<long, string>()
            : await _unitOfWork.SessionsRepo.GetSessionNamesByIdsAsync(teacherId, sessionIds);

        // Enrich the owning-teacher name — only meaningful here because this path can span
        // every teacher on the platform; the teacher-scoped list already knows its own name
        // client-side and doesn't need it.
        var teacherIds = students.Select(s => s.TeacherId).Distinct().ToList();
        IReadOnlyDictionary<long, string> teacherNames = teacherIds.Count == 0
            ? new Dictionary<long, string>()
            : await _unitOfWork.Users.GetTeacherNamesByIdsAsync(teacherIds);

        var dtos = students.Select(s => MapToDto(s, sessionNames, teacherNames)).ToList();

        var response = new PaginatedResponse<List<TeacherStudentDto>>
        {
            totalCount = totalCount,
            page = request.Page,
            pageSize = request.PageSize,
            totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize),
            data = dtos
        };

        return Result<PaginatedResponse<List<TeacherStudentDto>>>.Success(response, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<StudentCountsDto>> GetStudentCountsAsync(
        long teacherId, StudentListRequest request)
    {
        // Total active (no filters)
        int totalActive = await _unitOfWork.Students.CountActiveStudentsAsync(teacherId);

        // Recycle bin count
        int recycleBin = await _unitOfWork.Students.CountRecycleBinStudentsAsync(teacherId);

        // Filtered count (only if filters are actually applied)
        int filteredCount = totalActive;
        bool hasFilters = !string.IsNullOrWhiteSpace(request.Search)
                        || request.SessionId.HasValue
                        || request.MissingStudentPhone
                        || request.MissingParentPhone
                        || request.MissingSession;

        if (hasFilters)
        {
            var query = _unitOfWork.Students.BuildStudentListQuery(
                teacherId,
                request.Search,
                request.SessionId,
                request.MissingStudentPhone,
                request.MissingParentPhone,
                request.MissingSession);

            filteredCount = await _unitOfWork.Students.CountAsync(query);
        }

        var counts = new StudentCountsDto
        {
            TotalActiveStudents = totalActive,
            FilteredCount = filteredCount,
            RecycleBinCount = recycleBin
        };

        return Result<StudentCountsDto>.Success(counts, _localizer);
    }

    // ══════════════════════════════════════════════
    // SOFT DELETE (RECYCLE BIN)
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    /// <remarks>
    /// Soft delete is NOT just a flag flip. Before this fix the record kept its session
    /// assignment AND its student-ACCOUNT link stayed <c>LinkStatus.Active</c>, so:
    ///   • the student app kept listing the teacher (and their content) forever, and
    ///   • the filtered unique index (<c>[LinkStatus] IN (1,3)</c>) blocked the student from
    ///     ever sending a fresh link request to that teacher.
    /// The record still goes to the recycle bin (<c>IsDeleted</c>/<c>DeletedAt</c>, restorable
    /// for the 10-day retention window) — but it is unassigned and unlinked while it sits
    /// there. Restore brings the DATA back, never the link (see RestoreStudentAsync).
    /// </remarks>
    public async Task<Result<bool>> SoftDeleteStudentAsync(
        long teacherId, long studentId, long? actingUserId = null)
    {
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(studentId, teacherId);
        if (student is null)
            return Result<bool>.Failure(_localizer, "StudentNotFound", HttpStatusCode.NotFound);

        // The flag flip and the teardown must be atomic — a half-deleted student (hidden from
        // the roster but still linked to a live account) is exactly the broken state above.
        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync();

        StudentTeardownOutcome outcome;
        try
        {
            // REQ-STU-025: Move to recycle bin (soft-delete)
            student.IsDeleted = true;
            student.DeletedAt = DateTime.UtcNow;
            await _unitOfWork.Students.UpdateAsync(student);

            outcome = await _teardownService.UnassignAndUnlinkAsync(teacherId, student.Id, actingUserId);

            await _unitOfWork.SaveChangesAsync();
            if (ownsTransaction) await _unitOfWork.CommitAsync();
        }
        catch (Exception ex)
        {
            if (ownsTransaction) await _unitOfWork.RollbackAsync();
            _logger.LogError(ex,
                "Soft delete failed for student {StudentId} of teacher {TeacherId}; rolled back.",
                studentId, teacherId);
            return Result<bool>.Failure(_localizer, "StudentDeleteFailed", HttpStatusCode.InternalServerError);
        }

        // Post-commit, best-effort (§5.1) — never fails the delete.
        await _teardownService.NotifyStudentUnlinkedAsync(teacherId, outcome);

        return Result<bool>.Success(true, _localizer, "StudentDeletedSuccess");
    }

    /// <inheritdoc />
    /// <remarks>Same unassign + unlink teardown as the single soft delete, per student.</remarks>
    public async Task<Result<int>> BulkSoftDeleteStudentsAsync(
        long teacherId, BulkStudentIdsDto dto, long? actingUserId = null)
    {
        if (dto.StudentIds.Count == 0)
            return Result<int>.Failure(_localizer, "NoStudentsSelected", HttpStatusCode.BadRequest);

        var students = await _unitOfWork.Students.GetActiveByIdsAndTeacherAsync(teacherId, dto.StudentIds);

        if (students.Count == 0)
            return Result<int>.Failure(_localizer, "StudentNotFound", HttpStatusCode.NotFound);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync();

        var outcomes = new List<StudentTeardownOutcome>(students.Count);
        try
        {
            var now = DateTime.UtcNow;
            foreach (var student in students)
            {
                student.IsDeleted = true;
                student.DeletedAt = now;
                await _unitOfWork.Students.UpdateAsync(student);

                outcomes.Add(await _teardownService.UnassignAndUnlinkAsync(
                    teacherId, student.Id, actingUserId));
            }

            await _unitOfWork.SaveChangesAsync();
            if (ownsTransaction) await _unitOfWork.CommitAsync();
        }
        catch (Exception ex)
        {
            if (ownsTransaction) await _unitOfWork.RollbackAsync();
            _logger.LogError(ex,
                "Bulk soft delete failed for teacher {TeacherId}; rolled back {StudentCount} student(s).",
                teacherId, students.Count);
            return Result<int>.Failure(_localizer, "StudentDeleteFailed", HttpStatusCode.InternalServerError);
        }

        // Post-commit, best-effort (§5.1).
        foreach (var outcome in outcomes)
            await _teardownService.NotifyStudentUnlinkedAsync(teacherId, outcome);

        return Result<int>.Success(students.Count, _localizer, "BulkDeleteSuccess");
    }

    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<RecycleBinStudentDto>>>> GetRecycleBinAsync(
        long teacherId, int page = 1, int pageSize = 20)
    {
        var query = _unitOfWork.Students.BuildRecycleBinQuery(teacherId);
        int totalCount = await _unitOfWork.Students.CountAsync(query);

        var students = await _unitOfWork.Students.GetPagedAsync(query, page, pageSize);

        var now = DateTime.UtcNow;
        var dtos = students.Select(s => new RecycleBinStudentDto
        {
            Id = s.Id,
            StudentName = s.StudentName,
            StudentCode = s.StudentCode,
            StudentPhoneNumber = s.StudentPhoneNumber,
            ParentPhoneNumber = s.ParentPhoneNumber,
            DeletedAt = s.DeletedAt,
            // REQ-STU-UX-010: Days remaining = 10 - days since deletion
            DaysRemaining = s.DeletedAt.HasValue
                ? Math.Max(0, 10 - (int)(now - s.DeletedAt.Value).TotalDays)
                : 0
        }).ToList();

        var response = new PaginatedResponse<List<RecycleBinStudentDto>>
        {
            totalCount = totalCount,
            page = page,
            pageSize = pageSize,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
            data = dtos
        };

        return Result<PaginatedResponse<List<RecycleBinStudentDto>>>.Success(response, _localizer);
    }

    /// <inheritdoc />
    public async Task<Result<TeacherStudentDto>> RestoreStudentAsync(long teacherId, long studentId)
    {
        var student = await _unitOfWork.Students.GetByIdAndTeacherIgnoreFiltersAsync(studentId, teacherId);

        if (student is null || !student.IsDeleted)
            return Result<TeacherStudentDto>.Failure(_localizer, "RecycleBinStudentNotFound", HttpStatusCode.NotFound);

        // Check capacity before restoring
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher is not null)
        {
            int activeCount = await _unitOfWork.Students.CountActiveStudentsAsync(teacherId);
            if (activeCount >= teacher.StudentCapacity)
                return Result<TeacherStudentDto>.Failure(_localizer, "StudentCapacityReached", HttpStatusCode.BadRequest);

            var centerCapError = await CheckCenterStudentCapacityAsync(teacher, 1);
            if (centerCapError is not null)
                return Result<TeacherStudentDto>.Failure(_localizer, centerCapError, HttpStatusCode.Conflict);
        }

        // REQ-STU-031: Restore with all original data intact
        student.IsDeleted = false;
        student.DeletedAt = null;

        await _unitOfWork.Students.UpdateAsync(student);
        await _unitOfWork.SaveChangesAsync();

        return Result<TeacherStudentDto>.Success(MapToDto(student,null), _localizer, "StudentRestoredSuccess");
    }

    /// <inheritdoc />
    public async Task<Result<int>> BulkRestoreStudentsAsync(long teacherId, BulkStudentIdsDto dto)
    {
        if (dto.StudentIds.Count == 0)
            return Result<int>.Failure(_localizer, "NoStudentsSelected", HttpStatusCode.BadRequest);

        // Check capacity before restoring
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher is not null)
        {
            int activeCount = await _unitOfWork.Students.CountActiveStudentsAsync(teacherId);
            int remaining = teacher.StudentCapacity - activeCount;

            // Clamp by the center's remaining pool for center-owned teachers.
            var centerRemaining = await GetCenterRemainingStudentCapacityAsync(teacher);
            if (centerRemaining is not null)
                remaining = Math.Min(remaining, centerRemaining.Value);

            if (dto.StudentIds.Count > remaining)
                return Result<int>.Failure(_localizer,
                    teacher.CenterId is not null && centerRemaining is not null && centerRemaining.Value < dto.StudentIds.Count
                        ? "CenterStudentCapacityExhausted" : "StudentCapacityReached",
                    HttpStatusCode.BadRequest);
        }

        var students = await _unitOfWork.Students.GetDeletedByIdsAndTeacherAsync(teacherId, dto.StudentIds);

        if (students.Count == 0)
            return Result<int>.Failure(_localizer, "RecycleBinStudentNotFound", HttpStatusCode.NotFound);

        foreach (var student in students)
        {
            student.IsDeleted = false;
            student.DeletedAt = null;
            await _unitOfWork.Students.UpdateAsync(student);
        }

        await _unitOfWork.SaveChangesAsync();

        return Result<int>.Success(students.Count, _localizer, "BulkRestoreSuccess");
    }

    /// <inheritdoc />
    public async Task<Result<bool>> PermanentDeleteStudentAsync(long teacherId, long studentId)
    {
        var student = await _unitOfWork.Students.GetByIdAndTeacherIgnoreFiltersAsync(studentId, teacherId);

        if (student is null || !student.IsDeleted)
            return Result<bool>.Failure(_localizer, "RecycleBinStudentNotFound", HttpStatusCode.NotFound);

        bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction) await _unitOfWork.BeginTransactionAsync();

        try
        {
            // ── PAYMENT INTEGRATION: Sever student FK references on payment records ──
            // Denormalized StudentName/StudentCode preserved on the audit records (PaymentTransactions,
            // StudentDepartures, SessionTransferEvents, EventStudentObligations, EventPaymentTransactions).
            // PaymentPeriods (the student's monthly bills) are DELETED so they can't orphan into
            // dashboard aggregates; StudentPaymentCounter is deleted too.
            await _paymentService.OnStudentPermanentlyDeletedAsync(student.Id);

            // ── ATTENDANCE INTEGRATION: clear the student's session assignments, absence counters and
            // nullify attendance-record FKs BEFORE the hard delete. This hook existed but was never
            // invoked here, which left dangling StudentSessionAssignment rows that could later block
            // deletion of the session the purged student was assigned to (409 conflict).
            await _attendanceService.OnStudentPermanentlyDeletedAsync(student.Id);

            // ── VIDEO / ONLINE-EXAM / EXAM-HOMEWORK / LINK INTEGRATION ──
            // The modules that had NO purge hook at all. Their student FKs are non-nullable
            // NoAction/Restrict (VideoAnalytics, VideoWatchEvent, StudentOnlineExamReport,
            // StudentAssignmentObligation) or NoAction-nullable (VideoScope, VideoUnitScope), so
            // SQL Server cleans none of them: without this, permanently deleting any student who
            // had ever watched a video or been given an exam/homework threw an FK error (500).
            await _teardownService.PurgeStudentDependentsAsync(teacherId, student.Id);

            // WARNING: This is irreversible
            await _unitOfWork.Students.DeleteAsync(student);
            await _unitOfWork.SaveChangesAsync();

            if (ownsTransaction) await _unitOfWork.CommitAsync();
        }
        catch (Exception ex)
        {
            if (ownsTransaction) await _unitOfWork.RollbackAsync();
            _logger.LogError(ex,
                "Permanent delete failed for student {StudentId} of teacher {TeacherId}; rolled back.",
                studentId, teacherId);
            return Result<bool>.Failure(
                _localizer, "StudentPermanentDeleteFailed", HttpStatusCode.InternalServerError);
        }

        return Result<bool>.Success(true, _localizer, "StudentPermanentlyDeleted");
    }

    /// <inheritdoc />
    /// <remarks>
    /// Runs from the <c>recycle-bin-purge</c> Hangfire recurring job (registered in Program.cs).
    /// It had ZERO callers before that, so the advertised 10-day retention never actually
    /// expired anything and the recycle bin grew forever.
    ///
    /// IDEMPOTENT (§6.4): each pass re-reads the expired set, so a retry after a partial run
    /// simply purges whatever is left. Each student is purged in its OWN transaction (mirrors
    /// <c>AssistantCleanupJob</c>) so one residual FK / bad row can never abort the whole sweep —
    /// it just stays soft-deleted and is retried on the next run.
    /// </remarks>
    public async Task<int> PurgeExpiredRecycleBinRecordsAsync()
    {
        // REQ-STU-027/028: Purge records older than 10 days
        var expiredRecords = await _unitOfWork.Students.GetExpiredRecycleBinRecordsAsync();

        if (expiredRecords.Count == 0)
            return 0;

        int purged = 0;
        foreach (var student in expiredRecords)
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync();

                // ── PAYMENT / ATTENDANCE INTEGRATION: sever student FK references ──
                await _paymentService.OnStudentPermanentlyDeletedAsync(student.Id);
                await _attendanceService.OnStudentPermanentlyDeletedAsync(student.Id);

                // ── VIDEO / ONLINE-EXAM / EXAM-HOMEWORK / LINK INTEGRATION ──
                // Same blocking-FK cleanup as PermanentDeleteStudentAsync — without it the
                // sweep would throw on the first student who ever watched a video.
                await _teardownService.PurgeStudentDependentsAsync(student.TeacherId, student.Id);

                await _unitOfWork.Students.DeleteAsync(student);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                purged++;
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                // Swallow: the row stays in the recycle bin and is retried on the next sweep.
                // Logged so a row that keeps failing every night is visible instead of silent.
                _logger.LogError(ex,
                    "recycle-bin-purge: could not purge student {StudentId} of teacher {TeacherId}; " +
                    "left in the recycle bin for the next sweep.",
                    student.Id, student.TeacherId);
            }
        }

        return purged;
    }

    // ══════════════════════════════════════════════
    // BULK IMPORT
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    /// <summary>
    /// FIX GAP-1: Auto-generated codes now use the teacher's StudentCodeLanguage preference.
    /// FIX GAP-2: When StudentCodeGenerationMode == Manual, rows with blank codes are REJECTED
    ///            (consistent with single-entry behavior per REQ-STU-011.1) instead of silently
    ///            auto-generating them. This ensures the bulk import respects the same business
    ///            rules as the single-entry form.
    /// </summary>
    public async Task<Result<BulkImportResultDto>> BulkImportStudentsAsync(long teacherId, BulkImportTeacherStudentsDto dto)
    {
        // 1. Validate teacher exists
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher is null)
            return Result<BulkImportResultDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // NOTE: managerial does NOT block roster building (see CreateStudentAsync) — it only blocks
        // linking a student ACCOUNT to the teacher, which happens in the link-flow services.

        // 1b. Free-tier quota: bulk import is a subscriber feature — the free tier (1 student) is
        // served by single create only. Unsubscribed teachers must subscribe to import in bulk.
        if (!await _subscriptionGate.HasActiveSubscriptionAsync(teacherId))
            return Result<BulkImportResultDto>.Failure(
                _localizer, SubscriptionConstants.Messages.SubscriptionRequired, HttpStatusCode.Forbidden);

        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);

        // FIX GAP-1: Resolve the teacher's configured code generation language
        var codeLanguage = config?.StudentCodeLanguage ?? GenerationLanguage.English;

        // FIX GAP-2: Determine if the teacher uses manual code entry (EFFECTIVE mode — center default +
        // per-teacher override for center-owned teachers).
        bool isManualMode = await ResolveEffectiveCodeModeAsync(teacher, config) == GenerationMode.Manual;

        // Session assignment can be specified per-row (BulkImportStudentRowDto.SessionId) and/or as
        // an envelope-level default (dto.SessionId) applied to rows that omit their own. Each
        // distinct id is resolved to an owned session exactly once and cached; a null or non-owned
        // id is neglected (row imports unassigned) — per the "neglect if wrong session id" rule.
        // BR-SES-002: one session per student.
        var sessionCache = new Dictionary<long, Session?>();
        async Task<Session?> ResolveOwnedSessionAsync(long? sessionId)
        {
            if (!sessionId.HasValue) return null;
            if (sessionCache.TryGetValue(sessionId.Value, out var cached)) return cached;
            var resolved = await _unitOfWork.SessionsRepo.GetByIdAndTeacherAsync(sessionId.Value, teacherId);
            sessionCache[sessionId.Value] = resolved;
            return resolved;
        }

        // 2. Check capacity (per-teacher, then clamp by the center's remaining pool if center-owned)
        int activeCount = await _unitOfWork.Students.CountActiveStudentsAsync(teacherId);
        int remainingCapacity = teacher.StudentCapacity - activeCount;
        var centerRemainingImport = await GetCenterRemainingStudentCapacityAsync(teacher);
        if (centerRemainingImport is not null)
            remainingCapacity = Math.Min(remainingCapacity, centerRemainingImport.Value);

        var result = new BulkImportResultDto { TotalProcessed = dto.Students.Count };
        var validStudents = new List<TeacherStudent>();
        var usedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Student phone numbers claimed by earlier rows in THIS batch. The DB enforces
        // uniqueness of (TeacherId, StudentPhoneNumber); catching duplicates here as per-row
        // failures stops a single collision from aborting the whole transaction with one
        // opaque 409. Mirrors the within-batch student-code dedupe. ParentPhoneNumber is
        // deliberately NOT deduped — siblings on one roster legitimately share it.
        var usedStudentPhones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Resolved session per finalized student, consumed by the assignment hooks after insert.
        var studentSessions = new Dictionary<TeacherStudent, Session>();
        // Original 1-based sheet row number per finalized student, for the success report.
        var studentRowNumbers = new Dictionary<TeacherStudent, int>();
        // Auto-code rows are collected here and assigned codes in ONE batched generation after the
        // validation loop (see GenerateSequentialCodesAsync) — generating per row re-queries the DB
        // and spins, which made even a 4-row import take >60s on the Basic-tier DB.
        var pendingAutoCode = new List<TeacherStudent>();

        // 3. Validate each row
        for (int i = 0; i < dto.Students.Count; i++)
        {
            var row = dto.Students[i];
            int rowNumber = i + 1;

            // REQ-STU-018: Skip rows where StudentName is empty
            if (string.IsNullOrWhiteSpace(row.StudentName))
            {
                result.Failures.Add(new BulkImportFailureDto
                {
                    RowNumber = rowNumber,
                    StudentName = row.StudentName,
                    StudentCode = row.StudentCode,
                    Reason = _localizer["StudentNameRequired"]
                });
                continue;
            }

            // Normalize phones once, then validate FORMAT (STU-3) before the uniqueness/dedup check —
            // a malformed number is a more fundamental problem than a duplicate, and validating here
            // keeps bulk import consistent with single create/update and sign-up.
            string? rowStudentPhone = NormalizePhone(row.StudentPhoneNumber);
            string? rowParentPhone = NormalizePhone(row.ParentPhoneNumber);

            var rowPhoneError = ValidatePhoneNumbers(rowStudentPhone, rowParentPhone);
            if (rowPhoneError is not null)
            {
                result.Failures.Add(new BulkImportFailureDto
                {
                    RowNumber = rowNumber,
                    StudentName = row.StudentName,
                    StudentCode = row.StudentCode,
                    Reason = _localizer[rowPhoneError]
                });
                continue;
            }

            // Reject student phone numbers already claimed by an earlier row in this batch before
            // they reach the DB, where the unique index would otherwise abort the whole transaction.
            if (rowStudentPhone is not null && !usedStudentPhones.Add(rowStudentPhone))
            {
                result.Failures.Add(new BulkImportFailureDto
                {
                    RowNumber = rowNumber,
                    StudentName = row.StudentName,
                    StudentCode = row.StudentCode,
                    Reason = _localizer["StudentPhoneAlreadyExists"]
                });
                continue;
            }

            // Row-level session id wins; the envelope id is the default for rows that omit it.
            var rowSession = await ResolveOwnedSessionAsync(row.SessionId ?? dto.SessionId);

            // Resolve student code
            string studentCode;

            if (!string.IsNullOrWhiteSpace(row.StudentCode))
            {
                // STU-1: a manually supplied code is only accepted in Manual mode. In Auto mode the
                // code is system-generated and cannot be entered manually — reject the row (consistent
                // with single create/update) instead of silently applying the supplied code.
                if (!isManualMode)
                {
                    result.Failures.Add(new BulkImportFailureDto
                    {
                        RowNumber = rowNumber,
                        StudentName = row.StudentName,
                        StudentCode = row.StudentCode,
                        Reason = _localizer["StudentCodeNotAllowedAuto"]
                    });
                    continue;
                }

                // Manual mode: the code was provided in the import row — validate its format.
                // REQ-STU-CODE-006: Format validation for bulk import codes
                var codeValidation = ValidateStudentCodeFormat(row.StudentCode);
                if (codeValidation is not null)
                {
                    result.Failures.Add(new BulkImportFailureDto
                    {
                        RowNumber = rowNumber,
                        StudentName = row.StudentName,
                        StudentCode = row.StudentCode,
                        Reason = codeValidation
                    });
                    continue;
                }

                studentCode = row.StudentCode.Trim().ToUpperInvariant();

                // Check for duplicates within this import batch
                if (usedCodes.Contains(studentCode))
                {
                    result.Failures.Add(new BulkImportFailureDto
                    {
                        RowNumber = rowNumber,
                        StudentName = row.StudentName,
                        StudentCode = row.StudentCode,
                        Reason = _localizer["StudentCodeDuplicate"]
                    });
                    continue;
                }

                // REQ-STU-018: Detect and reject duplicate codes against existing DB records
                bool codeExists = await _unitOfWork.Students.StudentCodeExistsAsync(teacherId, studentCode);
                if (codeExists)
                {
                    result.Failures.Add(new BulkImportFailureDto
                    {
                        RowNumber = rowNumber,
                        StudentName = row.StudentName,
                        StudentCode = row.StudentCode,
                        Reason = _localizer["StudentCodeDuplicate"]
                    });
                    continue;
                }
            }
            else
            {
                // Code is blank in the import row

                // FIX GAP-2: When teacher config is Manual mode, a blank code is an error.
                // This is consistent with single-entry behavior (REQ-STU-011.1):
                // "the system shall not allow adding any students without entering a unique code"
                if (isManualMode)
                {
                    result.Failures.Add(new BulkImportFailureDto
                    {
                        RowNumber = rowNumber,
                        StudentName = row.StudentName,
                        StudentCode = row.StudentCode,
                        Reason = _localizer["StudentCodeRequiredManual"]
                    });
                    continue;
                }

                // Auto mode: defer code assignment. The code is generated for the whole batch in a
                // single DB read after this loop, then filled in below.
                var autoStudent = new TeacherStudent
                {
                    TeacherId = teacherId,
                    StudentName = row.StudentName.Trim(),
                    HashedToken = GenerateHashedToken(),
                    StudentPhoneNumber = rowStudentPhone,
                    ParentPhoneNumber = rowParentPhone,
                    SessionId = rowSession?.Id,
                    IsDeleted = false,
                    CreateAt = DateTime.UtcNow
                };
                validStudents.Add(autoStudent);
                pendingAutoCode.Add(autoStudent);
                studentRowNumbers[autoStudent] = rowNumber;
                if (rowSession is not null) studentSessions[autoStudent] = rowSession;
                continue;
            }

            usedCodes.Add(studentCode);

            // Create entity (manual / provided code)
            var student = new TeacherStudent
            {
                TeacherId = teacherId,
                StudentName = row.StudentName.Trim(),
                StudentCode = studentCode,
                HashedToken = GenerateHashedToken(),
                StudentPhoneNumber = rowStudentPhone,
                ParentPhoneNumber = rowParentPhone,
                Barcode = studentCode, // REQ-STU-054: Auto-generate barcode
                SessionId = rowSession?.Id,
                IsDeleted = false,
                CreateAt = DateTime.UtcNow
            };

            validStudents.Add(student);
            studentRowNumbers[student] = rowNumber;
            if (rowSession is not null) studentSessions[student] = rowSession;
        }

        // Assign auto-generated codes for all deferred rows in ONE batched DB read, skipping any that
        // collide with codes supplied manually elsewhere in this same batch.
        if (pendingAutoCode.Count > 0)
        {
            var generated = await _codeGenerator.GenerateSequentialCodesAsync(
                teacherId, pendingAutoCode.Count + usedCodes.Count, codeLanguage, teacher.CenterId);

            int gi = 0;
            foreach (var s in pendingAutoCode)
            {
                while (gi < generated.Count && usedCodes.Contains(generated[gi]))
                    gi++;
                if (gi >= generated.Count)
                    break; // safety — should never happen given the buffer above
                s.StudentCode = generated[gi];
                s.Barcode = generated[gi];
                usedCodes.Add(generated[gi]);
                gi++;
            }
        }

        // 4. Check if valid students exceed remaining capacity
        if (validStudents.Count > remainingCapacity)
        {
            return Result<BulkImportResultDto>.Failure(_localizer, "BulkImportExceedsCapacity", HttpStatusCode.BadRequest);
        }

        // 5. Insert all valid students in a single transaction
        if (validStudents.Count > 0)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                await _unitOfWork.Students.AddRangeAsync(validStudents);
                await _unitOfWork.SaveChangesAsync(); // materialize student Ids for the assignment hooks

                // Wire each imported student that resolved to a session into the attendance/payment
                // integration (assignment record + absence counter + payment periods). Without this
                // the student carries SessionId but never appears on the attendance roster, which is
                // driven by StudentSessionAssignments. Each student uses its own resolved session.
                if (studentSessions.Count > 0)
                {
                    foreach (var s in validStudents)
                    {
                        if (!studentSessions.TryGetValue(s, out var session))
                            continue;
                        await _attendanceService.OnStudentAssignedToSessionAsync(
                            teacherId, s.Id, session.Id, session.SessionName);
                        await _paymentService.OnStudentAssignedToSessionAsync(
                            teacherId, s.Id, session.Id, session.SessionName, DateTime.UtcNow);
                    }
                    await _unitOfWork.SaveChangesAsync();
                }

                await _unitOfWork.CommitAsync();
            }
            catch (DbUpdateException ex) when (ResolveUniqueViolationKey(ex) is { } messageKey)
            {
                // A phone/code collision against EXISTING (already-committed) students — the
                // within-batch dedupe above only catches duplicates inside this request. Roll back
                // and surface the specific field conflict instead of the generic 409 the exception
                // middleware would otherwise produce from a raw DbUpdateException.
                await _unitOfWork.RollbackAsync();
                return Result<BulkImportResultDto>.Failure(_localizer, messageKey, HttpStatusCode.Conflict);
            }
            catch
            {
                // REQ-STU-OFF-006: Full rollback on failure — no partial records saved
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        result.SuccessCount = validStudents.Count;
        result.FailedCount = result.Failures.Count;

        // Itemize the imported students (ids are materialized post-commit) so the client can render
        // a full "imported" list — with the resolved code and assigned session — beside the failures.
        result.Succeeded = validStudents
            .Select(s =>
            {
                studentSessions.TryGetValue(s, out var session);
                return new BulkImportSuccessDto
                {
                    RowNumber = studentRowNumbers.TryGetValue(s, out var rn) ? rn : 0,
                    StudentId = s.Id,
                    StudentName = s.StudentName,
                    StudentCode = s.StudentCode,
                    SessionId = session?.Id,
                    SessionName = session?.SessionName
                };
            })
            .OrderBy(x => x.RowNumber)
            .ToList();

        return Result<BulkImportResultDto>.Success(result, _localizer, "BulkImportComplete");
    }

    /// <inheritdoc />
    public async Task<Result<SessionAssignmentChipsDto>> GetSessionAssignmentChipsAsync(
        long teacherId, string? search)
    {
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher is null)
            return Result<SessionAssignmentChipsDto>.Failure(
                _localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // Student counts (All / Unassigned / per-session), scoped to the search term.
        var counts = await _unitOfWork.Students.GetAssignmentCountsAsync(teacherId, search);

        // Full, stable chip catalog: every session, ordered by name.
        var sessions = await _unitOfWork.SessionsRepo.GetTeacherSessionNamesAsync(teacherId);

        var countMap = counts.PerSession.ToDictionary(x => x.SessionId, x => x.AssignedCount);

        var dto = new SessionAssignmentChipsDto
        {
            TotalCount = counts.CountAll,
            UnassignedCount = counts.CountUnassigned,
            Sessions = sessions.Select(s => new SessionChipDto
            {
                SessionId = s.Id,
                SessionName = s.SessionName,
                AssignedCount = countMap.TryGetValue(s.Id, out var c) ? c : 0
            }).ToList()
        };

        return Result<SessionAssignmentChipsDto>.Success(dto, _localizer);
    }
    /// <inheritdoc />
    public async Task<Result<TenantStudentListDto>> GetTenantStudentListAsync(
        long teacherId, StudentListRequest request)
    {
        // Reuse the existing list pipeline verbatim (teacher validation, filter, sort,
        // pagination, and session-name enrichment). It returns TeacherNotFound(404)
        // when the teacher does not exist — propagate that as-is.
        var listResult = await GetStudentListAsync(teacherId, request);
        if (!listResult.IsSuccess)
            return Result<TenantStudentListDto>.Failure(listResult.Message, listResult.StatusCode);

        // Unfiltered active total for the screen header (REQ-STU-UX-001) — constant
        // regardless of the search/filter applied to the paginated list above.
        int tenantTotal = await _unitOfWork.Students.CountActiveStudentsAsync(teacherId);

        var dto = new TenantStudentListDto
        {
            noOfStudentsForTenant = tenantTotal,
            students = listResult.Data!
        };

        return Result<TenantStudentListDto>.Success(dto, _localizer);
    }

    // ══════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════

    /// <summary>
    /// Maps a TeacherStudent to the profile DTO, attaching the assigned-session card
    /// (name / occurrence / payment / cost). <paramref name="session"/> is null when unassigned.
    /// </summary>
    private static TeacherStudentProfileDto MapToProfileDto(
        TeacherStudent student, SessionSummaryRow? session)
    {
        var dto = new TeacherStudentProfileDto();
        PopulateBase(dto, student, session?.SessionName);

        if (session is not null)
        {
            dto.AssignedSession = new AssignedSessionSummaryDto
            {
                SessionId = session.Id,
                SessionName = session.SessionName,
                OccurrenceType = session.OccurrenceType,
                PaymentType = session.PaymentType,
                SessionAmount = session.SessionAmount
            };
        }

        return dto;
    }

    /// <summary>Shared base-field mapping for every TeacherStudent output DTO.</summary>
    private static void PopulateBase(
          TeacherStudentDto dto, TeacherStudent student, string? sessionName,
          string? teacherName = null, long? linkId = null)
    {
        dto.Id = student.Id;
        dto.TeacherId = student.TeacherId;
        dto.StudentName = student.StudentName;
        dto.StudentCode = student.StudentCode;
        dto.HashedToken = student.HashedToken;
        dto.StudentPhoneNumber = student.StudentPhoneNumber;
        dto.ParentPhoneNumber = student.ParentPhoneNumber;
        dto.Barcode = student.StudentCode; // canonical scan key (see StudentBarcodeService)
        dto.SessionId = student.SessionId;
        dto.SessionName = sessionName;
        dto.TeacherName = teacherName;
        dto.CreatedAt = student.CreateAt;
        // REQ-STU-UX-007: Complete = all optional fields filled
        dto.IsComplete = !string.IsNullOrWhiteSpace(student.StudentPhoneNumber)
                      && !string.IsNullOrWhiteSpace(student.ParentPhoneNumber)
                      && student.SessionId.HasValue;
    }

    private static string? ResolveSessionName(
        TeacherStudent student, IReadOnlyDictionary<long, string>? sessionNames)
    {
        if (student.SessionId.HasValue && sessionNames is not null
            && sessionNames.TryGetValue(student.SessionId.Value, out var name))
            return name;
        return null;
    }


    /// Resolves the Id of whichever Active <c>StudentTeacherLink</c> currently claims the given
    /// roster record, or null when none does. Single-record wrapper around
    /// <c>IUserRepo.GetActiveLinkIdsByTeacherStudentIdsAsync</c> — the same lookup already used
    /// by the SuperAdmin student list — so every response carrying a <see cref="TeacherStudentDto"/>
    /// or <see cref="TeacherStudentProfileDto"/> has a trustworthy LinkId for the Unlink action.
    /// FIX (BUG-LINKID-01): previously only the admin list path populated LinkId; the profile
    /// and update responses always returned null, so the frontend's Unbind call had no valid
    /// target — it 404'd silently while the link's TeacherStudentId FK was never cleared.
    /// </summary>
    private async Task<long?> ResolveActiveLinkIdAsync(long teacherStudentId)
    {
        var activeLinkIds = await _unitOfWork.Users
            .GetActiveLinkIdsByTeacherStudentIdsAsync(new[] { teacherStudentId });
        return activeLinkIds.TryGetValue(teacherStudentId, out var linkId) ? linkId : (long?)null;
    }
    /// <summary>
    /// Maps a TeacherStudent to the output DTO, resolving the assigned-session
    /// display name from the supplied Id→name map (list path). SessionName stays
    /// null when the student is unassigned or the map is not provided.
    /// </summary>
    /// <summary>

    private static TeacherStudentDto MapToDto(
          TeacherStudent student,
          IReadOnlyDictionary<long, string>? sessionNames,
          IReadOnlyDictionary<long, string>? teacherNames = null,
          IReadOnlyDictionary<long, long>? activeLinkIds = null)
    {
        string? sessionName = null;
        if (student.SessionId.HasValue && sessionNames is not null
            && sessionNames.TryGetValue(student.SessionId.Value, out var name))
        {
            sessionName = name;
        }

        string? teacherName = null;
        if (teacherNames is not null && teacherNames.TryGetValue(student.TeacherId, out var tName))
        {
            teacherName = tName;
        }
        long? linkId = null;
        if (activeLinkIds is not null && activeLinkIds.TryGetValue(student.Id, out var lId))
        {
            linkId = lId;
        }

        return new TeacherStudentDto
        {
            Id = student.Id,
            TeacherId = student.TeacherId,
            StudentName = student.StudentName,
            StudentCode = student.StudentCode,
            HashedToken = student.HashedToken,
            StudentPhoneNumber = student.StudentPhoneNumber,
            LinkId = linkId,
            ParentPhoneNumber = student.ParentPhoneNumber,
            Barcode = student.StudentCode, // canonical scan key (see StudentBarcodeService)
            SessionId = student.SessionId,
            SessionName = sessionName,
            TeacherName = teacherName,
            CreatedAt = student.CreateAt,
            // REQ-STU-UX-007: Complete = all optional fields filled
            IsComplete = !string.IsNullOrWhiteSpace(student.StudentPhoneNumber)
                      && !string.IsNullOrWhiteSpace(student.ParentPhoneNumber)
                      && student.SessionId.HasValue
        };
    }

    /// <summary>
    /// Validates the format of a manually entered student code.
    /// REQ-STU-CODE-001: 1-10 alphanumeric characters (A-Z, a-z, 0-9 only).
    /// Returns a localized error message if invalid, null if valid.
    /// </summary>
    private string? ValidateStudentCodeFormat(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return _localizer["StudentCodeRequiredManual"];

        string trimmed = code.Trim();

        // REQ-STU-CODE-005: Specific error for length violation
        if (trimmed.Length > 10)
            return _localizer["StudentCodeTooLong"];

        // REQ-STU-CODE-005: Specific error for disallowed characters
        if (!StudentCodeFormatRegex.IsMatch(trimmed))
            return _localizer["StudentCodeInvalidFormat"];

        return null;
    }

    /// <summary>
    /// Generates a cryptographically secure hashed token for a student.
    /// REQ-STU-004: "Student Hashed Password" — auto-generated.
    /// AAM-NFR-03: Cryptographically unique and collision-resistant.
    /// Uses the same approach as StudentAccountCodeGenerator.
    /// </summary>
    private static string GenerateHashedToken()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        const int length = 16;
        var tokenChars = new char[length];

        for (int i = 0; i < length; i++)
        {
            int index = RandomNumberGenerator.GetInt32(0, chars.Length);
            tokenChars[i] = chars[index];
        }

        return new string(tokenChars);
    }
    /// <summary>
    /// Normalizes an optional phone number: trims whitespace and collapses blank/empty
    /// input to null so it stays out of the filtered unique index
    /// (IX_TeacherStudents_TeacherId_*PhoneNumber, filtered WHERE ... IS NOT NULL).
    /// Without this, an empty string "" would be indexed and two blank entries would collide.
    /// </summary>
    private static string? NormalizePhone(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// STU-3: Validates the optional student/parent phone numbers against the Egyptian-mobile rule
    /// enforced at sign-up (<see cref="UserService.PhoneNumberValidator"/>: 11 digits, 010/011/012/015).
    /// Both fields are optional, so a null/blank value is skipped; a supplied value must be well-formed.
    /// Returns the localized-message KEY for the first invalid number (student checked before parent),
    /// or null when both numbers are absent or valid. Shared by create, update, and bulk import so all
    /// three roster-entry paths reject the same malformed input consistently.
    /// </summary>
    private static string? ValidatePhoneNumbers(string? studentPhone, string? parentPhone)
    {
        if (!string.IsNullOrWhiteSpace(studentPhone)
            && !PhoneNumberValidator.IsValidEgyptianMobile(studentPhone.Trim()))
            return "StudentPhoneInvalidFormat";

        if (!string.IsNullOrWhiteSpace(parentPhone)
            && !PhoneNumberValidator.IsValidEgyptianMobile(parentPhone.Trim()))
            return "ParentPhoneInvalidFormat";

        return null;
    }

    /// <summary>
    /// Detects a SQL Server unique-key violation (2601/2627) on the TeacherStudent phone
    /// indexes and maps it to the matching localized message key. Returns null when the
    /// exception is not a phone unique-violation (so the exception rethrows unchanged).
    /// Matches on the column name embedded in the index name, so it is independent of the
    /// exact index database name. Mirrors the IsUniqueViolation pattern in SubscriptionService.
    /// </summary>
    private static string? ResolveUniqueViolationKey(DbUpdateException ex)
    {
        var sql = ex.InnerException as Microsoft.Data.SqlClient.SqlException
                  ?? ex.GetBaseException() as Microsoft.Data.SqlClient.SqlException;

        if (sql is not { Number: 2601 or 2627 })
            return null;

        string message = sql.Message;

        if (message.Contains("StudentPhoneNumber", StringComparison.OrdinalIgnoreCase))
            return "StudentPhoneAlreadyExists";
        if (message.Contains("StudentCode", StringComparison.OrdinalIgnoreCase))
            return "StudentCodeDuplicate";

        // Not a known business-level unique index (e.g. a primary-key collision from an
        // identity-seed desync). Returning null lets the exception rethrow with its real detail
        // instead of masquerading as a phone conflict — the old generic "PhoneAlreadyExists"
        // fallback is exactly what turned a StudentCode collision into a bogus phone error.
        return null;
    }
    /// <inheritdoc />
    public async Task<Result<PaginatedResponse<List<TeacherStudentDto>>>> GetStudentListForAdminAsync(
        long? teacherId, StudentListRequest request)
    {
        // Validate teacher exists — only when one was supplied. Null teacherId = every teacher,
        // safe only because the controller gates this to SuperAdmin (roleOnly).
        if (teacherId.HasValue)
        {
            var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId.Value);
            if (teacher is null)
                return Result<PaginatedResponse<List<TeacherStudentDto>>>.Failure(
                    _localizer, "TeacherNotFound", HttpStatusCode.NotFound);
        }

        var query = _unitOfWork.Students.BuildStudentListQuery(
            teacherId,
            request.Search,
            request.SessionId,
            request.MissingStudentPhone,
            request.MissingParentPhone,
            request.MissingSession,
            request.SortBy,
            request.SortDirection);

        int totalCount = await _unitOfWork.Students.CountAsync(query);
        var students = await _unitOfWork.Students.GetPagedAsync(query, request.Page, request.PageSize);

        // Enrich the assigned-session badge. sessionIds are globally unique (Session.Id is the PK,
        // not composite with TeacherId), so one cross-tenant lookup is safe even when the page mixes
        // students from different teachers.
        var sessionIds = students
            .Where(s => s.SessionId.HasValue)
            .Select(s => s.SessionId!.Value)
            .Distinct()
            .ToList();

        IReadOnlyDictionary<long, string> sessionNames = sessionIds.Count == 0
            ? new Dictionary<long, string>()
            : await _unitOfWork.SessionsRepo.GetSessionNamesByIdsAsync(teacherId, sessionIds);

        // Enrich the owning-teacher name — only meaningful here because this path can span
        // every teacher on the platform; the teacher-scoped list already knows its own name
        // client-side and doesn't need it.
        var teacherIds = students.Select(s => s.TeacherId).Distinct().ToList();
        IReadOnlyDictionary<long, string> teacherNames = teacherIds.Count == 0
            ? new Dictionary<long, string>()
            : await _unitOfWork.Users.GetTeacherNamesByIdsAsync(teacherIds);
        var studentIds = students.Select(s => s.Id).ToList();
        IReadOnlyDictionary<long, long> activeLinkIds = studentIds.Count == 0
            ? new Dictionary<long, long>()
            : await _unitOfWork.Users.GetActiveLinkIdsByTeacherStudentIdsAsync(studentIds);

        var dtos = students.Select(s => MapToDto(s, sessionNames, teacherNames,activeLinkIds)).ToList();

        var response = new PaginatedResponse<List<TeacherStudentDto>>
        {
            totalCount = totalCount,
            page = request.Page,
            pageSize = request.PageSize,
            totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize),
            data = dtos
        };

        return Result<PaginatedResponse<List<TeacherStudentDto>>>.Success(response, _localizer);
    }
}