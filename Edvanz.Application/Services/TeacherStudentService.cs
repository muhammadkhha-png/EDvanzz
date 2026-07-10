using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.TeacherStudent;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Constants;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

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
        IStringLocalizer<Domain.Resources.Messages> localizer)
    {
        _unitOfWork = unitOfWork;
        _codeGenerator = codeGenerator;
        _paymentService = paymentService;
        _subscriptionGate = subscriptionGate;
        _attendanceService = attendanceService;
        _localizer = localizer;
    }

    // ══════════════════════════════════════════════
    // SINGLE STUDENT CRUD
    // ══════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<TeacherStudentDto>> CreateStudentAsync(long teacherId, CreateTeacherStudentDto dto)
    {
        // 1. Validate teacher exists
        var teacher = await _unitOfWork.Users.GetActiveTeacherByIdAsync(teacherId);
        if (teacher is null)
            return Result<TeacherStudentDto>.Failure(_localizer, "TeacherNotFound", HttpStatusCode.NotFound);

        // 1b. Free-tier quota: unsubscribed teachers may keep at most Quotas.Students students.
        if (!await _subscriptionGate.HasActiveSubscriptionAsync(teacherId))
        {
            int freeTierCount = await _unitOfWork.Students.CountActiveStudentsAsync(teacherId);
            if (freeTierCount >= _subscriptionGate.Quotas.Students)
                return Result<TeacherStudentDto>.Failure(
                    _localizer, SubscriptionConstants.Messages.SubscriptionRequired, HttpStatusCode.Forbidden);
        }

        // 2. Validate student name is not empty (REQ-STU-014)
        if (string.IsNullOrWhiteSpace(dto.StudentName))
            return Result<TeacherStudentDto>.Failure(_localizer, "StudentNameRequired", HttpStatusCode.BadRequest);

        // 3. Check student capacity limit
        int activeCount = await _unitOfWork.Students.CountActiveStudentsAsync(teacherId);
        if (activeCount >= teacher.StudentCapacity)
            return Result<TeacherStudentDto>.Failure(_localizer, "StudentCapacityReached", HttpStatusCode.BadRequest);

        // 4. Resolve student code based on teacher configuration
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);
        string studentCode;

        if (config?.StudentCodeGenerationMode == GenerationMode.Manual)
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
            studentCode = await _codeGenerator.GenerateNextCodeAsync(teacherId, codeLanguage);
        }

        // 5. Generate hashed token (auto-generated, REQ-STU-004)
        string hashedToken = GenerateHashedToken();

        // 6. Generate barcode (REQ-STU-047: encodes the student code)
        string barcode = studentCode; // Barcode data = student code per REQ-STU-047

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
            SessionId = dto.SessionId,
            IsDeleted = false,
            CreateAt = DateTime.UtcNow
        };

        // REPLACE
        try
        {
            await _unitOfWork.Students.AddAsync(student);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ResolveUniqueViolationKey(ex) is { } messageKey)
        {
            return Result<TeacherStudentDto>.Failure(_localizer, messageKey, HttpStatusCode.Conflict);
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

        return Result<TeacherStudentProfileDto>.Success(MapToProfileDto(student, session), _localizer);
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

        // 3. Handle student code update (if code is provided)
        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);

        if (config?.StudentCodeGenerationMode == GenerationMode.Manual && !string.IsNullOrWhiteSpace(dto.StudentCode))
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
        }
        // If auto-generate mode, student code is NOT editable (REQ-STU-048 spirit: immutable auto-codes)

        // 4. Update fields
        student.StudentName = dto.StudentName.Trim();
        student.StudentPhoneNumber = NormalizePhone(dto.StudentPhoneNumber);
        student.ParentPhoneNumber = NormalizePhone(dto.ParentPhoneNumber);
        student.SessionId = dto.SessionId;
        // REQ-STU-048: Barcode NEVER changes even if other fields are modified

        try
        {
            await _unitOfWork.Students.UpdateAsync(student);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ResolveUniqueViolationKey(ex) is { } messageKey)
        {
            return Result<TeacherStudentDto>.Failure(_localizer, messageKey, HttpStatusCode.Conflict);
        }

        return Result<TeacherStudentDto>.Success(MapToDto(student,null), _localizer, "StudentUpdatedSuccess");
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

        var dtos = students.Select(s => MapToDto(s, sessionNames)).ToList();

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
    public async Task<Result<bool>> SoftDeleteStudentAsync(long teacherId, long studentId)
    {
        var student = await _unitOfWork.Students.GetActiveByIdAndTeacherAsync(studentId, teacherId);
        if (student is null)
            return Result<bool>.Failure(_localizer, "StudentNotFound", HttpStatusCode.NotFound);

        // REQ-STU-025: Move to recycle bin (soft-delete)
        student.IsDeleted = true;
        student.DeletedAt = DateTime.UtcNow;

        await _unitOfWork.Students.UpdateAsync(student);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, "StudentDeletedSuccess");
    }

    /// <inheritdoc />
    public async Task<Result<int>> BulkSoftDeleteStudentsAsync(long teacherId, BulkStudentIdsDto dto)
    {
        if (dto.StudentIds.Count == 0)
            return Result<int>.Failure(_localizer, "NoStudentsSelected", HttpStatusCode.BadRequest);

        var students = await _unitOfWork.Students.GetActiveByIdsAndTeacherAsync(teacherId, dto.StudentIds);

        if (students.Count == 0)
            return Result<int>.Failure(_localizer, "StudentNotFound", HttpStatusCode.NotFound);

        var now = DateTime.UtcNow;
        foreach (var student in students)
        {
            student.IsDeleted = true;
            student.DeletedAt = now;
            await _unitOfWork.Students.UpdateAsync(student);
        }

        await _unitOfWork.SaveChangesAsync();

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

            if (dto.StudentIds.Count > remaining)
                return Result<int>.Failure(_localizer, "StudentCapacityReached", HttpStatusCode.BadRequest);
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

        // ── PAYMENT INTEGRATION: Nullify student FK references on all payment records ──
        // Denormalized StudentName/StudentCode preserved on PaymentTransactions, PaymentPeriods,
        // StudentDepartures, SessionTransferEvents, EventStudentObligations, EventPaymentTransactions.
        // StudentPaymentCounter is deleted (no longer needed after purge).
        await _paymentService.OnStudentPermanentlyDeletedAsync(student.Id);

        // ── ATTENDANCE INTEGRATION: clear the student's session assignments, absence counters and
        // nullify attendance-record FKs BEFORE the hard delete. This hook existed but was never
        // invoked here, which left dangling StudentSessionAssignment rows that could later block
        // deletion of the session the purged student was assigned to (409 conflict).
        await _attendanceService.OnStudentPermanentlyDeletedAsync(student.Id);

        // WARNING: This is irreversible
        await _unitOfWork.Students.DeleteAsync(student);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true, _localizer, "StudentPermanentlyDeleted");
    }

    /// <inheritdoc />
    public async Task<int> PurgeExpiredRecycleBinRecordsAsync()
    {
        // REQ-STU-027/028: Purge records older than 10 days
        var expiredRecords = await _unitOfWork.Students.GetExpiredRecycleBinRecordsAsync();

        if (expiredRecords.Count == 0)
            return 0;

        // ── PAYMENT INTEGRATION: Nullify student FK references before bulk purge ──
        foreach (var student in expiredRecords)
        {
            await _paymentService.OnStudentPermanentlyDeletedAsync(student.Id);
            await _attendanceService.OnStudentPermanentlyDeletedAsync(student.Id);
        }

        await _unitOfWork.Students.DeleteRangeAsync(expiredRecords);
        await _unitOfWork.SaveChangesAsync();

        return expiredRecords.Count;
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

        // 1b. Free-tier quota: bulk import is a subscriber feature — the free tier (1 student) is
        // served by single create only. Unsubscribed teachers must subscribe to import in bulk.
        if (!await _subscriptionGate.HasActiveSubscriptionAsync(teacherId))
            return Result<BulkImportResultDto>.Failure(
                _localizer, SubscriptionConstants.Messages.SubscriptionRequired, HttpStatusCode.Forbidden);

        var config = await _unitOfWork.Users.GetConfigurationByTeacherIdAsync(teacherId);

        // FIX GAP-1: Resolve the teacher's configured code generation language
        var codeLanguage = config?.StudentCodeLanguage ?? GenerationLanguage.English;

        // FIX GAP-2: Determine if the teacher uses manual code entry
        bool isManualMode = config?.StudentCodeGenerationMode == GenerationMode.Manual;

        // 2. Check capacity
        int activeCount = await _unitOfWork.Students.CountActiveStudentsAsync(teacherId);
        int remainingCapacity = teacher.StudentCapacity - activeCount;

        var result = new BulkImportResultDto { TotalProcessed = dto.Students.Count };
        var validStudents = new List<TeacherStudent>();
        var usedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

            // Resolve student code
            string studentCode;

            if (!string.IsNullOrWhiteSpace(row.StudentCode))
            {
                // Code was provided in the import row — validate it regardless of mode
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
                    StudentPhoneNumber = NormalizePhone(row.StudentPhoneNumber),
                    ParentPhoneNumber = NormalizePhone(row.ParentPhoneNumber),
                    IsDeleted = false,
                    CreateAt = DateTime.UtcNow
                };
                validStudents.Add(autoStudent);
                pendingAutoCode.Add(autoStudent);
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
                StudentPhoneNumber = NormalizePhone(row.StudentPhoneNumber),
                ParentPhoneNumber = NormalizePhone(row.ParentPhoneNumber),
                Barcode = studentCode, // REQ-STU-054: Auto-generate barcode
                IsDeleted = false,
                CreateAt = DateTime.UtcNow
            };

            validStudents.Add(student);
        }

        // Assign auto-generated codes for all deferred rows in ONE batched DB read, skipping any that
        // collide with codes supplied manually elsewhere in this same batch.
        if (pendingAutoCode.Count > 0)
        {
            var generated = await _codeGenerator.GenerateSequentialCodesAsync(
                teacherId, pendingAutoCode.Count + usedCodes.Count, codeLanguage);

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
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
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
    private static void PopulateBase(TeacherStudentDto dto, TeacherStudent student, string? sessionName)
    {
        dto.Id = student.Id;
        dto.TeacherId = student.TeacherId;
        dto.StudentName = student.StudentName;
        dto.StudentCode = student.StudentCode;
        dto.HashedToken = student.HashedToken;
        dto.StudentPhoneNumber = student.StudentPhoneNumber;
        dto.ParentPhoneNumber = student.ParentPhoneNumber;
        dto.Barcode = student.Barcode;
        dto.SessionId = student.SessionId;
        dto.SessionName = sessionName;
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

    /// <summary>
    /// Maps a TeacherStudent to the output DTO, resolving the assigned-session
    /// display name from the supplied Id→name map (list path). SessionName stays
    /// null when the student is unassigned or the map is not provided.
    /// </summary>
    private static TeacherStudentDto MapToDto(
        TeacherStudent student, IReadOnlyDictionary<long, string>? sessionNames)
    {
        string? sessionName = null;
        if (student.SessionId.HasValue && sessionNames is not null
            && sessionNames.TryGetValue(student.SessionId.Value, out var name))
        {
            sessionName = name;
        }

        return new TeacherStudentDto
        {
            Id = student.Id,
            TeacherId = student.TeacherId,
            StudentName = student.StudentName,
            StudentCode = student.StudentCode,
            HashedToken = student.HashedToken,
            StudentPhoneNumber = student.StudentPhoneNumber,
            ParentPhoneNumber = student.ParentPhoneNumber,
            Barcode = student.Barcode,
            SessionId = student.SessionId,
            SessionName = sessionName,
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
        if (message.Contains("ParentPhoneNumber", StringComparison.OrdinalIgnoreCase))
            return "ParentPhoneAlreadyExists";
        if (message.Contains("StudentCode", StringComparison.OrdinalIgnoreCase))
            return "StudentCodeDuplicate";

        // Not a known business-level unique index (e.g. a primary-key collision from an
        // identity-seed desync). Returning null lets the exception rethrow with its real detail
        // instead of masquerading as a phone conflict — the old generic "PhoneAlreadyExists"
        // fallback is exactly what turned a StudentCode collision into a bogus phone error.
        return null;
    }
}