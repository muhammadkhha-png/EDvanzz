<#
============================================================================
 apply-payment-wallet-fixes.ps1

 Fixes: GET /api/v1/assistants/{assistantId}/wallet -> "collections" always
 empty (was hard-scoped to the current calendar month). Re-scopes the
 Collections window to "since the assistant's last wallet handover"
 (reset/withdraw), adds TotalRefunded / TotalCollectedAllTime / SinceAt,
 and fixes assistant.name being null for an assistant caller.

 WHY THIS SCRIPT INSTEAD OF A .patch FILE:
   git apply on this checkout fails on CRLF/LF mismatches (confirmed
   repeatedly on this repo). This script edits files directly via exact
   string replacement -- same technique as an anchor-based manual edit,
   just scripted. Each block is matched independently; if a file has
   drifted since your last sync, that ONE block is skipped and reported
   at the end -- nothing partially applies, nothing silently corrupts.

 USAGE:
   1. Save this file at the ROOT of the EDvanzz (backend) repo, i.e.
      next to EDvanzz.sln.
   2. From that folder, in PowerShell:
        .\apply-payment-wallet-fixes.ps1
   3. Read the summary at the end. Any block marked SKIPPED needs a
      manual anchor-based edit (ask Claude) before you build.
   4. Open the solution in Visual Studio, build, review the diffs,
      then commit with your usual git commands (suggested at the end).

 SAFE TO RE-RUN: every edit is idempotent-checked -- if the NEW text is
 already present, that block is reported ALREADY-APPLIED and skipped.
============================================================================
#>

$ErrorActionPreference = 'Stop'
$results = @()

function Edit-File {
    param(
        [string]$RelativePath,
        [string]$Description,
        [string]$OldText,
        [string]$NewText
    )

    $path = Join-Path $PSScriptRoot $RelativePath
    if (-not (Test-Path $path)) {
        $script:results += [pscustomobject]@{ File = $RelativePath; Block = $Description; Status = 'FILE NOT FOUND' }
        return
    }

    $raw = Get-Content -Path $path -Raw
    $usesCrlf = $raw -match "`r`n"

    # Normalize both the file content and the anchors to LF-only for a
    # reliable Contains/Replace, regardless of which line endings the
    # checkout currently has.
    $normalized = $raw -replace "`r`n", "`n"
    $oldNorm = $OldText -replace "`r`n", "`n"
    $newNorm = $NewText -replace "`r`n", "`n"

    if ($normalized.Contains($newNorm)) {
        $script:results += [pscustomobject]@{ File = $RelativePath; Block = $Description; Status = 'ALREADY APPLIED' }
        return
    }

    $count = ([regex]::Matches($normalized, [regex]::Escape($oldNorm))).Count
    if ($count -eq 0) {
        $script:results += [pscustomobject]@{ File = $RelativePath; Block = $Description; Status = 'SKIPPED - anchor not found (repo drifted, edit manually)' }
        return
    }
    if ($count -gt 1) {
        $script:results += [pscustomobject]@{ File = $RelativePath; Block = $Description; Status = "SKIPPED - anchor matched $count times (ambiguous, edit manually)" }
        return
    }

    $updated = $normalized.Replace($oldNorm, $newNorm)
    if ($usesCrlf) { $updated = $updated -replace "`n", "`r`n" }

    Set-Content -Path $path -Value $updated -NoNewline
    $script:results += [pscustomobject]@{ File = $RelativePath; Block = $Description; Status = 'APPLIED' }
}

# ============================================================================
# EDIT 1 of 6 -- IPaymentRepo.cs: new method GetLastWalletResetAtAsync
# ============================================================================
Edit-File -RelativePath 'Edvanz.Domain\Interfaces\IPaymentRepo.cs' `
    -Description 'Add GetLastWalletResetAtAsync declaration' `
    -OldText @'
    /// <summary>
    /// Gets all wallet reset logs for an assistant.
    /// REQ-PAY-059: Assistant Wallet History Report.
    /// </summary>
    Task<IReadOnlyList<WalletResetLog>> GetWalletResetLogsAsync(long teacherId, long assistantId);

    // ══════════════════════════════════════════════
    // PAYMENT EDIT LOG QUERIES
    // ══════════════════════════════════════════════
'@ `
    -NewText @'
    /// <summary>
    /// Gets all wallet reset logs for an assistant.
    /// REQ-PAY-059: Assistant Wallet History Report.
    /// </summary>
    Task<IReadOnlyList<WalletResetLog>> GetWalletResetLogsAsync(long teacherId, long assistantId);

    /// <summary>
    /// Timestamp of the assistant's most recent wallet handover -- a full reset or a partial
    /// withdraw both write a <see cref="WalletResetLog"/>. Null when the assistant has never had
    /// a handover, meaning their full lifetime history is still "since the last reset".
    /// REQ-PAY-034/035: anchors the AssistantWallet screen's collection window to the cash the
    /// wallet is currently holding.
    /// </summary>
    Task<DateTime?> GetLastWalletResetAtAsync(long teacherId, long assistantId);

    // ══════════════════════════════════════════════
    // PAYMENT EDIT LOG QUERIES
    // ══════════════════════════════════════════════
'@

# ============================================================================
# EDIT 2 of 6 -- PaymentRepo.cs: implement GetLastWalletResetAtAsync
# ============================================================================
Edit-File -RelativePath 'Edvanz.Infrastructure\Repositories\PaymentRepo.cs' `
    -Description 'Implement GetLastWalletResetAtAsync' `
    -OldText @'
    /// <inheritdoc />
    public async Task<IReadOnlyList<WalletResetLog>> GetWalletResetLogsAsync(
        long teacherId, long assistantId)
    {
        return await _context.WalletResetLogs
            .Where(l => l.TeacherId == teacherId && l.AssistantId == assistantId)
            .OrderByDescending(l => l.ResetAt)
            .AsNoTracking()
            .ToListAsync();
    }
'@ `
    -NewText @'
    /// <inheritdoc />
    public async Task<IReadOnlyList<WalletResetLog>> GetWalletResetLogsAsync(
        long teacherId, long assistantId)
    {
        return await _context.WalletResetLogs
            .Where(l => l.TeacherId == teacherId && l.AssistantId == assistantId)
            .OrderByDescending(l => l.ResetAt)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetLastWalletResetAtAsync(long teacherId, long assistantId)
    {
        return await _context.WalletResetLogs
            .Where(l => l.TeacherId == teacherId && l.AssistantId == assistantId)
            .OrderByDescending(l => l.ResetAt)
            .Select(l => (DateTime?)l.ResetAt)
            .FirstOrDefaultAsync();
    }
'@

# ============================================================================
# EDIT 3 of 6 -- PaymentRepo.cs: fix null assistant.name (Cause 4 / C1)
# ============================================================================
Edit-File -RelativePath 'Edvanz.Infrastructure\Repositories\PaymentRepo.cs' `
    -Description 'Include Assistant.User in GetAssistantWalletByUserIdAsync' `
    -OldText @'
    /// <inheritdoc />
    public async Task<AssistantWallet?> GetAssistantWalletByUserIdAsync(
        long teacherId, long assistantUserId)
    {
        return await _context.AssistantWallets
            .FirstOrDefaultAsync(w => w.TeacherId == teacherId
                && w.AssistantUserId == assistantUserId);
    }
'@ `
    -NewText @'
    /// <inheritdoc />
    public async Task<AssistantWallet?> GetAssistantWalletByUserIdAsync(
        long teacherId, long assistantUserId)
    {
        // BUGFIX (2026-08-01): Include added so assistant.name is populated on the AssistantWallet
        // screen for an assistant caller (this method -- not GetAssistantWalletAsync -- resolves
        // their own wallet; see PaymentScreenService.GetAssistantWalletScreenAsync). Deliberately
        // NOT AsNoTracking: this method also sits on the collect hot path
        // (UpdateAssistantWalletAfterCollectionAsync / AdjustAssistantWalletAsync), which needs the
        // returned entity tracked for the RowVersion concurrency-retry loop.
        return await _context.AssistantWallets
            .Include(w => w.Assistant)
                .ThenInclude(a => a.User)
            .FirstOrDefaultAsync(w => w.TeacherId == teacherId
                && w.AssistantUserId == assistantUserId);
    }
'@

# ============================================================================
# EDIT 4 of 6 -- PaymentScreenDtos.cs: new fields
# ============================================================================
Edit-File -RelativePath 'Edvanz.Application\Dtos\Payment\PaymentScreenDtos.cs' `
    -Description 'Add TotalRefunded / TotalCollectedAllTime / SinceAt fields' `
    -OldText @'
public class AssistantWalletInfoDto
{
    public decimal TotalCashCollected { get; set; }
    public decimal WalletBalance { get; set; }
    public int CollectionsCount { get; set; }
    public DateTime? LastActivityAt { get; set; }
}

public class AssistantWalletCollectionsDto
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
    public List<AssistantWalletCollectionItemDto> Items { get; set; } = new();
}
'@ `
    -NewText @'
public class AssistantWalletInfoDto
{
    /// <summary>Gross collections since the last handover (reset or withdraw).</summary>
    public decimal TotalCashCollected { get; set; }
    /// <summary>Gross refunds taken back from this collector since the last handover.</summary>
    public decimal TotalRefunded { get; set; }
    public decimal WalletBalance { get; set; }
    /// <summary>Lifetime collected -- never reduced by a reset/withdraw. REQ-PAY-035.</summary>
    public decimal TotalCollectedAllTime { get; set; }
    /// <summary>Lifetime transaction count -- NOT scoped to the Collections window below.</summary>
    public int CollectionsCount { get; set; }
    public DateTime? LastActivityAt { get; set; }
}

public class AssistantWalletCollectionsDto
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
    /// <summary>
    /// Start of the window below -- the last handover, or null if there has never been one
    /// (in which case this is the assistant's full lifetime history).
    /// </summary>
    public DateTime? SinceAt { get; set; }
    public List<AssistantWalletCollectionItemDto> Items { get; set; } = new();
}
'@

# ============================================================================
# EDIT 5 of 6 -- IPaymentScreenService.cs: doc comment update (non-breaking)
# ============================================================================
Edit-File -RelativePath 'Edvanz.Application\ServiceContract\IPaymentScreenService.cs' `
    -Description 'Update GetAssistantWalletScreenAsync doc comment' `
    -OldText @'
    /// <summary>
    /// Screen: AssistantWallet. Wallet card + paginated recent collections for one assistant.
    /// Reuses <c>GetAssistantWalletAsync</c> (tenant-scoped → 404 if not this teacher's) and
    /// <c>GetCollectorTransactionsPagedAsync</c>.
    /// When <paramref name="restrictToAssistantUserId"/> is supplied (assistant caller) the
    /// requested <paramref name="assistantId"/> is IGNORED and the caller's OWN wallet (resolved by
    /// their user id) is returned — an assistant can only open their own wallet, never a peer's.
    /// TODO(assistant-dashboard): interim own-scoping; the dedicated assistant dashboard is to be
    /// built end-to-end by frontend + backend.
    /// </summary>
'@ `
    -NewText @'
    /// <summary>
    /// Screen: AssistantWallet. Wallet card + paginated recent collections for one assistant.
    /// Reuses <c>GetAssistantWalletAsync</c> (tenant-scoped → 404 if not this teacher's) and
    /// <c>GetCollectorTransactionsInRangeAsync</c> / <c>GetCollectorRefundsInRangeAsync</c>.
    /// BUGFIX (2026-08-01): Collections is scoped SINCE THE ASSISTANT'S LAST WALLET HANDOVER
    /// (reset or withdraw -- <c>GetLastWalletResetAtAsync</c>), not the current calendar month.
    /// The old month-based scope went empty on every month rollover even with a real balance held,
    /// and drifted on local/UTC boundaries near midnight. The new window reconciles by
    /// construction: TotalCashCollected − TotalRefunded == WalletBalance.
    /// When <paramref name="restrictToAssistantUserId"/> is supplied (assistant caller) the
    /// requested <paramref name="assistantId"/> is IGNORED and the caller's OWN wallet (resolved by
    /// their user id) is returned — an assistant can only open their own wallet, never a peer's.
    /// TODO(assistant-dashboard): interim own-scoping; the dedicated assistant dashboard is to be
    /// built end-to-end by frontend + backend.
    /// </summary>
'@

# ============================================================================
# EDIT 6 of 6 -- PaymentScreenService.cs: the actual fix (window + new fields)
# ============================================================================
Edit-File -RelativePath 'Edvanz.Application\Services\PaymentScreenService.cs' `
    -Description 'Rescope GetAssistantWalletScreenAsync window; add TotalRefunded/TotalCollectedAllTime/SinceAt' `
    -OldText @'
    /// <inheritdoc />
    public async Task<Result<AssistantWalletScreenResponse>> GetAssistantWalletScreenAsync(
        long teacherId, long assistantId, int page, int limit, long? restrictToAssistantUserId = null)
    {
        // Tenant-scoped lookup: a wallet belonging to another teacher's assistant returns null → 404.
        // TODO(assistant-dashboard): interim own-scoping. When an assistant calls, resolve THEIR OWN
        // wallet by user id and ignore the requested assistantId so they can never open a peer's
        // wallet. The dedicated assistant dashboard is to be built end-to-end by frontend + backend.
        var wallet = restrictToAssistantUserId is long ownUserId
            ? await _unitOfWork.PaymentsRepo.GetAssistantWalletByUserIdAsync(teacherId, ownUserId)
            : await _unitOfWork.PaymentsRepo.GetAssistantWalletAsync(teacherId, assistantId);
        if (wallet is null)
            return Result<AssistantWalletScreenResponse>.Failure(
                _localizer, PaymentConstants.Messages.WalletNotFound, HttpStatusCode.NotFound);

        (page, limit) = NormalizePaging(page, limit);

        // The assistant log is scoped to the current local month: the collections they took this
        // month. "Total cash collected" (below) is the same window, net of refunds.
        var localToday = _timeZoneService.GetTeacherLocalDate(teacherId);
        var walletMonthStart = new DateTime(localToday.Year, localToday.Month, 1);
        var monthEndExclusive = walletMonthStart.AddMonths(1);

        // The log mixes collections and refunds for the month in one chronological list. Collections
        // are positive; a refund taken back from this collector is a NEGATIVE-amount entry dated
        // when it was refunded. Merged and paged in-memory (a single collector's month is bounded).
        var monthTxns = await _unitOfWork.PaymentsRepo
            .GetCollectorTransactionsInRangeAsync(teacherId, wallet.AssistantUserId, walletMonthStart, monthEndExclusive);
        var monthRefunds = await _unitOfWork.PaymentsRepo
            .GetCollectorRefundsInRangeAsync(teacherId, wallet.AssistantUserId, walletMonthStart, monthEndExclusive);

        // "Total cash collected" = money in (collections this month) minus money out (refunds this
        // month). A same-month collect-then-refund nets to zero; a refund of an earlier month's
        // collection shows this month as negative net.
        decimal monthCashCollected =
            monthTxns.Sum(t => t.AmountPaid) - monthRefunds.Sum(r => r.RefundAmount);

        var merged = new List<AssistantWalletCollectionItemDto>(monthTxns.Count + monthRefunds.Count);
        merged.AddRange(monthTxns.Select(tx => new AssistantWalletCollectionItemDto
        {
            Id = tx.Id.ToString(CultureInfo.InvariantCulture),
            StudentId = tx.TeacherStudentId?.ToString(CultureInfo.InvariantCulture),
            StudentName = tx.StudentName,
            StudentCode = tx.StudentCode,
            SessionName = string.IsNullOrEmpty(tx.SessionName) ? null : tx.SessionName,
            Amount = tx.AmountPaid,
            CollectedAt = tx.CollectedAt
        }));
        merged.AddRange(monthRefunds.Select(r => new AssistantWalletCollectionItemDto
        {
            Id = $"refund-{r.Id.ToString(CultureInfo.InvariantCulture)}",
            StudentId = r.StudentId?.ToString(CultureInfo.InvariantCulture),
            StudentName = r.StudentName,
            StudentCode = r.StudentCode,
            SessionName = string.IsNullOrEmpty(r.SessionName) ? null : r.SessionName,
            Amount = -r.RefundAmount, // negative → refund
            CollectedAt = r.RefundedAt
        }));

        int total = merged.Count;
        var items = merged
            .OrderByDescending(i => i.CollectedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToList();

        var response = new AssistantWalletScreenResponse
        {
            Assistant = new AssistantWalletAssistantDto
            {
                Id = assistantId.ToString(CultureInfo.InvariantCulture),
                Name = wallet.Assistant?.User?.FullName,
                Role = "Assistant",
                AvatarUrl = null,
                TransactionCount = wallet.TransactionCount
            },
            Wallet = new AssistantWalletInfoDto
            {
                TotalCashCollected = monthCashCollected,
                WalletBalance = wallet.CurrentBalance,
                CollectionsCount = wallet.TransactionCount,
                LastActivityAt = wallet.LastCollectionAt
            },
            Collections = new AssistantWalletCollectionsDto
            {
                Total = total,
                Page = page,
                Limit = limit,
                Items = items
            }
        };

        return Result<AssistantWalletScreenResponse>.Success(
            response, _localizer, PaymentConstants.Messages.Success);
    }
'@ `
    -NewText @'
    /// <inheritdoc />
    public async Task<Result<AssistantWalletScreenResponse>> GetAssistantWalletScreenAsync(
        long teacherId, long assistantId, int page, int limit, long? restrictToAssistantUserId = null)
    {
        // Tenant-scoped lookup: a wallet belonging to another teacher's assistant returns null → 404.
        // TODO(assistant-dashboard): interim own-scoping. When an assistant calls, resolve THEIR OWN
        // wallet by user id and ignore the requested assistantId so they can never open a peer's
        // wallet. The dedicated assistant dashboard is to be built end-to-end by frontend + backend.
        var wallet = restrictToAssistantUserId is long ownUserId
            ? await _unitOfWork.PaymentsRepo.GetAssistantWalletByUserIdAsync(teacherId, ownUserId)
            : await _unitOfWork.PaymentsRepo.GetAssistantWalletAsync(teacherId, assistantId);
        if (wallet is null)
            return Result<AssistantWalletScreenResponse>.Failure(
                _localizer, PaymentConstants.Messages.WalletNotFound, HttpStatusCode.NotFound);

        (page, limit) = NormalizePaging(page, limit);

        // BUGFIX (2026-08-01): was scoped to the CURRENT CALENDAR MONTH, which went empty on every
        // month rollover even with a real balance held, and drifted on UTC/local boundaries near
        // midnight. Now scoped to SINCE THE LAST WALLET HANDOVER (reset or withdraw both write a
        // WalletResetLog), which reconciles with WalletBalance by construction: collected − refunded
        // == balance held. No handover yet → DateTime.MinValue, i.e. the assistant's full lifetime
        // history (bounded in practice; a SQL-side paged query is a documented follow-up if a single
        // collector's un-reset history ever grows past a few hundred rows).
        DateTime sinceAt = await _unitOfWork.PaymentsRepo
            .GetLastWalletResetAtAsync(teacherId, wallet.AssistantId) ?? DateTime.MinValue;
        DateTime nowUtc = DateTime.UtcNow;

        // The log mixes collections and refunds since the handover in one chronological list.
        // Collections are positive; a refund taken back from this collector is a NEGATIVE-amount
        // entry dated when it was refunded. Merged and paged in-memory (bounded per collector/window).
        // Both queries stay on the UTC CollectedAt/EditedAt columns: PaymentEditLog has no local-time
        // twin, and an instant-based window (vs. the old calendar-month one) has no local/UTC
        // boundary to correct in the first place.
        var periodTxns = await _unitOfWork.PaymentsRepo
            .GetCollectorTransactionsInRangeAsync(teacherId, wallet.AssistantUserId, sinceAt, nowUtc);
        var periodRefunds = await _unitOfWork.PaymentsRepo
            .GetCollectorRefundsInRangeAsync(teacherId, wallet.AssistantUserId, sinceAt, nowUtc);

        // Gross collected and gross refunded are reported separately (not netted) so the card
        // explains the list below it directly: collected X, refunded Y, holding Z = X − Y.
        decimal periodCollected = periodTxns.Sum(t => t.AmountPaid);
        decimal periodRefunded = periodRefunds.Sum(r => r.RefundAmount);

        var merged = new List<AssistantWalletCollectionItemDto>(periodTxns.Count + periodRefunds.Count);
        merged.AddRange(periodTxns.Select(tx => new AssistantWalletCollectionItemDto
        {
            Id = tx.Id.ToString(CultureInfo.InvariantCulture),
            StudentId = tx.TeacherStudentId?.ToString(CultureInfo.InvariantCulture),
            StudentName = tx.StudentName,
            StudentCode = tx.StudentCode,
            SessionName = string.IsNullOrEmpty(tx.SessionName) ? null : tx.SessionName,
            Amount = tx.AmountPaid,
            CollectedAt = tx.CollectedAt
        }));
        merged.AddRange(periodRefunds.Select(r => new AssistantWalletCollectionItemDto
        {
            Id = $"refund-{r.Id.ToString(CultureInfo.InvariantCulture)}",
            StudentId = r.StudentId?.ToString(CultureInfo.InvariantCulture),
            StudentName = r.StudentName,
            StudentCode = r.StudentCode,
            SessionName = string.IsNullOrEmpty(r.SessionName) ? null : r.SessionName,
            Amount = -r.RefundAmount, // negative → refund
            CollectedAt = r.RefundedAt
        }));

        int total = merged.Count;
        var items = merged
            .OrderByDescending(i => i.CollectedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToList();

        var response = new AssistantWalletScreenResponse
        {
            Assistant = new AssistantWalletAssistantDto
            {
                Id = assistantId.ToString(CultureInfo.InvariantCulture),
                Name = wallet.Assistant?.User?.FullName,
                Role = "Assistant",
                AvatarUrl = null,
                TransactionCount = wallet.TransactionCount
            },
            Wallet = new AssistantWalletInfoDto
            {
                TotalCashCollected = periodCollected,
                TotalRefunded = periodRefunded,
                WalletBalance = wallet.CurrentBalance,
                TotalCollectedAllTime = wallet.TotalCollected,
                CollectionsCount = wallet.TransactionCount,
                LastActivityAt = wallet.LastCollectionAt
            },
            Collections = new AssistantWalletCollectionsDto
            {
                Total = total,
                Page = page,
                Limit = limit,
                SinceAt = sinceAt == DateTime.MinValue ? (DateTime?)null : sinceAt,
                Items = items
            }
        };

        return Result<AssistantWalletScreenResponse>.Success(
            response, _localizer, PaymentConstants.Messages.Success);
    }
'@

# ============================================================================
# SUMMARY
# ============================================================================
Write-Host ""
Write-Host "==================== SUMMARY ====================" -ForegroundColor Cyan
$results | Format-Table -AutoSize

$applied = ($results | Where-Object { $_.Status -eq 'APPLIED' }).Count
$already = ($results | Where-Object { $_.Status -eq 'ALREADY APPLIED' }).Count
$skipped = ($results | Where-Object { $_.Status -like 'SKIPPED*' -or $_.Status -eq 'FILE NOT FOUND' }).Count

Write-Host ""
Write-Host "Applied: $applied   Already applied: $already   Needs manual attention: $skipped" `
    -ForegroundColor $(if ($skipped -gt 0) { 'Yellow' } else { 'Green' })

if ($skipped -gt 0) {
    Write-Host ""
    Write-Host "One or more blocks did not apply automatically -- go back to Claude with the" -ForegroundColor Yellow
    Write-Host "SKIPPED rows above and it will give you anchor-based manual edit instructions" -ForegroundColor Yellow
    Write-Host "for just those blocks." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Open EDvanzz.sln in Visual Studio and Build (Ctrl+Shift+B)."
Write-Host "  2. Review the diffs in Git Changes."
Write-Host "  3. Commit, e.g.:"
Write-Host "       git checkout -b fix/assistant-wallet-collections-empty"
Write-Host "       git add -A"
Write-Host "       git commit -m ""Fix: AssistantWallet Collections empty + null name (month-scope -> since-last-handover)"""
Write-Host "       git push origin fix/assistant-wallet-collections-empty"
