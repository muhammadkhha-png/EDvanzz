<#
.SYNOPSIS
    Unknown Inventory Flow Refactoring - Phase 1 (Q1/Q2 only).
    Anchor-based find/replace, not a git patch - safe against CRLF checkout mismatches.

.DESCRIPTION
    Implements exactly what Q1 (Stock stays branch-scoped entitlement) and Q2 (Unknown-way
    transfers become entitlement-only, no ProductItem touched) resolved:

      1. TransferService.CreateAsync - Unknown-way lines become a pure Stock-to-Stock
         entitlement move. No ProductItem selected/touched, no CardTransferItem rows, no
         Hold, settles immediately. A transfer made entirely of Unknown-way lines closes as
         Received right away instead of sitting InProgress forever.
      2. TransferService.SettleAsync / DisposeAsync - exclude already-settled (Unknown-way)
         lines from the settlement contract, so a mixed Known+Unknown transfer's remaining
         Known-way lines can still be received/disposed normally without the caller having
         to account for a line that already closed at creation.
      3. BatchUploadService - stop writing ProductItem.BranchID for Unknown-way rows at
         ingestion and re-sight; the row's branch still names who gets the Stock credit.
         The pre-existing "BranchID is null => CardInTransit" re-upload guard is narrowed to
         Known-way only, since null is now an Unknown-way card's normal resting state.

    NOT included (still open - Q3/Q4/Q5/Q6/Q7/Q8 in the analysis doc):
      - Printing endpoint/service (new, greenfield)
      - PAN-based disposal for Unknown-way (needs Q7 confirmation)
      - ProductItem.RowVersion / concurrency (Q4)
      - Retroactive backfill of existing Unknown-way BranchID values (Q5)
      - New unassigned-pool index (additive migration, safe to add anytime - not scripted
        here since it's a straight `dotnet ef migrations add`, not a source edit)

.NOTES
    Run from the repo root (BelalMuhamed/inventory). Idempotent: if an anchor is already
    gone (i.e. already applied), that step is skipped with a warning, not a failure.
#>

$ErrorActionPreference = 'Stop'

function Apply-Edit {
    param(
        [string]$Path,
        [string]$Old,
        [string]$New,
        [string]$Description
    )

    if (-not (Test-Path $Path)) {
        throw "File not found: $Path"
    }

    $content = Get-Content -Path $Path -Raw

    $count = ([regex]::Matches($content, [regex]::Escape($Old))).Count

    if ($count -eq 0) {
        Write-Warning "SKIP  [$Description] - anchor not found in $Path (already applied, or file has diverged - check manually)."
        return
    }
    if ($count -gt 1) {
        throw "ABORT [$Description] - anchor found $count times in $Path, expected exactly 1. Refusing to guess which one."
    }

    $updated = $content.Replace($Old, $New)
    Set-Content -Path $Path -Value $updated -NoNewline
    Write-Host "OK    [$Description] - applied to $Path"
}

$transferServicePath = Join-Path $PSScriptRoot 'InfrastructureLayer\Services\TransferService.cs'
$batchUploadServicePath = Join-Path $PSScriptRoot 'InfrastructureLayer\Services\BatchUploadService.cs'

# =====================================================================================
# Edit 1 of 6 - TransferService.CreateAsync: Unknown-way lines become entitlement-only.
# =====================================================================================

$edit1Old = @'
                    foreach ((CreateTransferLine line, Product product) in lines)
                    {
                        IReadOnlyList<ProductItem> selected;

                        if (product.ProductTransactionWay == ProductTransactionWay.Known)
                        {
                            IReadOnlyDictionary<long, ProductItem> found = await _unitOfWork.ProductItems
                                .GetManyForUpdateAsync(tenantId, line.ProductItemIds!, cancellationToken);

                            var picked = new List<ProductItem>(line.ProductItemIds!.Count);
                            foreach (long itemId in line.ProductItemIds)
                            {
                                if (!found.TryGetValue(itemId, out ProductItem? card))
                                    return Result.Failure<CardTransfer>(TransferErrors.ItemNotFound(itemId));
                                if (card.ProductId != product.Id)
                                    return Result.Failure<CardTransfer>(TransferErrors.ItemProductMismatch(card.MaskedPan));
                                if (card.BranchID != source.Id)
                                    return Result.Failure<CardTransfer>(TransferErrors.ItemNotAtSourceBranch(card.MaskedPan));
                                if (card.Status != CardStatus.Available)
                                    return Result.Failure<CardTransfer>(TransferErrors.ItemNotAvailable(card.MaskedPan));
                                picked.Add(card);
                            }
                            selected = picked;
                        }
                        else
                        {
                            selected = await _unitOfWork.ProductItems.GetAvailableForUpdateAsync(
                                tenantId, source.Id, product.Id, line.TransactedQuantity, cancellationToken);

                            if (selected.Count < line.TransactedQuantity)
                                return Result.Failure<CardTransfer>(TransferErrors.StockInconsistency(source.Id, product.Id));
                        }

                        // Pull every selected card out of the source: it is in transit now, at no
                        // branch, until settlement pins it somewhere (decision Q4).
                        foreach (ProductItem card in selected)
                        {
                            card.BranchID = null;
                            card.Status = CardStatus.OnHold;
                        }

                        var productLine = new CardTransferProduct
                        {
                            TenantId = tenantId,
                            ProductId = product.Id,
                            TransactedQuantity = line.TransactedQuantity,
                            ProductTransactionWay = product.ProductTransactionWay,   // snapshot
                        };
                        transfer.Products.Add(productLine);

                        foreach (ProductItem card in selected)
                        {
                            transfer.Items.Add(new CardTransferItem
                            {
                                TenantId = tenantId,
                                ProductItemId = card.ID,
                                ReceiveStatus = TransactionItemReceiveStatus.Pending,
                            });
                        }

                        // The only stock movement at create time: the whole line leaves the
                        // source's Available and enters its Hold. The target is untouched until
                        // settlement — nothing is "received" yet.
                        Stock sourceStock = await _unitOfWork.Stocks.GetOrCreateForUpdateAsync(
                            tenantId, source.Id, product.Id, cancellationToken);

                        int updatedAvailable = sourceStock.AvailableQuantity - line.TransactedQuantity;
                        if (updatedAvailable < 0)
                            return Result.Failure<CardTransfer>(StockErrors.InsufficientAvailable(source.Id, product.Id));

                        sourceStock.AvailableQuantity = updatedAvailable;
                        sourceStock.HoldQuantity += line.TransactedQuantity;
                        sourceStock.UpdatedAt = DateTime.UtcNow;
                    }
'@

$edit1New = @'
                    bool anyOpenLine = false;

                    foreach ((CreateTransferLine line, Product product) in lines)
                    {
                        if (product.ProductTransactionWay == ProductTransactionWay.Unknown)
                        {
                            // Unknown Inventory Refactor (decisions Q1/Q2): a transfer moves Stock
                            // *entitlement* only. No ProductItem is selected, touched, or
                            // reassigned — physical cards stay BranchID = null and are only ever
                            // pinned to a branch at print or disposal, keyed by PAN. There is
                            // nothing physically in transit, so the line settles immediately
                            // (RealQuantityReceived = TransactedQuantity) rather than entering the
                            // usual Hold -> receive lifecycle.
                            Stock unknownSourceStock = await _unitOfWork.Stocks.GetOrCreateForUpdateAsync(
                                tenantId, source.Id, product.Id, cancellationToken);

                            int updatedAvailable = unknownSourceStock.AvailableQuantity - line.TransactedQuantity;
                            if (updatedAvailable < 0)
                                return Result.Failure<CardTransfer>(StockErrors.InsufficientAvailable(source.Id, product.Id));

                            unknownSourceStock.AvailableQuantity = updatedAvailable;
                            unknownSourceStock.UpdatedAt = DateTime.UtcNow;

                            Stock unknownTargetStock = await _unitOfWork.Stocks.GetOrCreateForUpdateAsync(
                                tenantId, target.Id, product.Id, cancellationToken);
                            unknownTargetStock.AvailableQuantity += line.TransactedQuantity;
                            unknownTargetStock.UpdatedAt = DateTime.UtcNow;

                            transfer.Products.Add(new CardTransferProduct
                            {
                                TenantId = tenantId,
                                ProductId = product.Id,
                                TransactedQuantity = line.TransactedQuantity,
                                ProductTransactionWay = product.ProductTransactionWay,   // snapshot
                                RealQuantityReceived = line.TransactedQuantity,          // settled now - entitlement only
                                DisposedQuantity = 0,
                            });

                            continue;   // no ProductItem/CardTransferItem rows for this line
                        }

                        anyOpenLine = true;

                        IReadOnlyDictionary<long, ProductItem> found = await _unitOfWork.ProductItems
                            .GetManyForUpdateAsync(tenantId, line.ProductItemIds!, cancellationToken);

                        var picked = new List<ProductItem>(line.ProductItemIds!.Count);
                        foreach (long itemId in line.ProductItemIds)
                        {
                            if (!found.TryGetValue(itemId, out ProductItem? card))
                                return Result.Failure<CardTransfer>(TransferErrors.ItemNotFound(itemId));
                            if (card.ProductId != product.Id)
                                return Result.Failure<CardTransfer>(TransferErrors.ItemProductMismatch(card.MaskedPan));
                            if (card.BranchID != source.Id)
                                return Result.Failure<CardTransfer>(TransferErrors.ItemNotAtSourceBranch(card.MaskedPan));
                            if (card.Status != CardStatus.Available)
                                return Result.Failure<CardTransfer>(TransferErrors.ItemNotAvailable(card.MaskedPan));
                            picked.Add(card);
                        }
                        IReadOnlyList<ProductItem> selected = picked;

                        // Pull every selected card out of the source: it is in transit now, at no
                        // branch, until settlement pins it somewhere (decision Q4).
                        foreach (ProductItem card in selected)
                        {
                            card.BranchID = null;
                            card.Status = CardStatus.OnHold;
                        }

                        var productLine = new CardTransferProduct
                        {
                            TenantId = tenantId,
                            ProductId = product.Id,
                            TransactedQuantity = line.TransactedQuantity,
                            ProductTransactionWay = product.ProductTransactionWay,   // snapshot
                        };
                        transfer.Products.Add(productLine);

                        foreach (ProductItem card in selected)
                        {
                            transfer.Items.Add(new CardTransferItem
                            {
                                TenantId = tenantId,
                                ProductItemId = card.ID,
                                ReceiveStatus = TransactionItemReceiveStatus.Pending,
                            });
                        }

                        // The only stock movement at create time: the whole line leaves the
                        // source's Available and enters its Hold. The target is untouched until
                        // settlement — nothing is "received" yet.
                        Stock sourceStock = await _unitOfWork.Stocks.GetOrCreateForUpdateAsync(
                            tenantId, source.Id, product.Id, cancellationToken);

                        int updatedKnownAvailable = sourceStock.AvailableQuantity - line.TransactedQuantity;
                        if (updatedKnownAvailable < 0)
                            return Result.Failure<CardTransfer>(StockErrors.InsufficientAvailable(source.Id, product.Id));

                        sourceStock.AvailableQuantity = updatedKnownAvailable;
                        sourceStock.HoldQuantity += line.TransactedQuantity;
                        sourceStock.UpdatedAt = DateTime.UtcNow;
                    }

                    // A transfer made up entirely of Unknown-way (entitlement-only) lines has
                    // nothing left to receive — close it out immediately rather than leaving it
                    // InProgress forever with no Known-way remainder anyone will ever call
                    // receive/dispose on. A transfer with at least one Known-way line still opens
                    // InProgress as before.
                    if (!anyOpenLine)
                    {
                        transfer.TransactionStatus = TransactionStatus.Received;
                        transfer.StatusChangedAt = DateTime.UtcNow;
                    }
'@

Apply-Edit -Path $transferServicePath -Old $edit1Old -New $edit1New `
    -Description '1/6 TransferService.CreateAsync: Unknown-way -> entitlement-only'

# =====================================================================================
# Edit 2 of 6 - SettleAsync: validate against still-open lines only.
# =====================================================================================

$edit2Old = @'
            // ---- Validate the settlement covers exactly the transfer's lines, one-to-one. -----
            foreach (long productId in settlements.Keys)
            {
                if (transfer.Products.All(p => p.ProductId != productId))
                    return Result.Failure<SettleTransferResult>(TransferErrors.UnknownProductInSettlement(productId));
            }
            foreach (CardTransferProduct line in transfer.Products)
            {
                if (!settlements.ContainsKey(line.ProductId))
                    return Result.Failure<SettleTransferResult>(TransferErrors.MissingProductInSettlement(line.ProductId));
            }
'@

$edit2New = @'
            // ---- Validate the settlement covers exactly the transfer's still-open lines. -------
            // Unknown-way lines settle immediately at creation (Unknown Inventory Refactor,
            // decisions Q1/Q2) - RealQuantityReceived is already non-null for them by the time
            // this method runs, so they are excluded from the settlement contract entirely: the
            // caller neither supplies nor sees a settlement entry for a line that never had
            // anything physically in transit to receive.
            IReadOnlyList<CardTransferProduct> openLines = transfer.Products
                .Where(p => p.RealQuantityReceived is null)
                .ToList();

            foreach (long productId in settlements.Keys)
            {
                if (openLines.All(p => p.ProductId != productId))
                    return Result.Failure<SettleTransferResult>(TransferErrors.UnknownProductInSettlement(productId));
            }
            foreach (CardTransferProduct line in openLines)
            {
                if (!settlements.ContainsKey(line.ProductId))
                    return Result.Failure<SettleTransferResult>(TransferErrors.MissingProductInSettlement(line.ProductId));
            }
'@

Apply-Edit -Path $transferServicePath -Old $edit2Old -New $edit2New `
    -Description '2/6 TransferService.SettleAsync: validate against openLines'

# =====================================================================================
# Edit 3 of 6 - SettleAsync: main plan-building loop over openLines, not every line.
# =====================================================================================

$edit3Old = @'
            foreach (CardTransferProduct line in transfer.Products)
            {
                LineSettlement s = settlements[line.ProductId];
                int received = s.Received;
'@

$edit3New = @'
            foreach (CardTransferProduct line in openLines)
            {
                LineSettlement s = settlements[line.ProductId];
                int received = s.Received;
'@

Apply-Edit -Path $transferServicePath -Old $edit3Old -New $edit3New `
    -Description '3/6 TransferService.SettleAsync: plan-building loop over openLines'

# =====================================================================================
# Edit 4 of 6 - DisposeAsync: only build a dispose-everything plan for open lines.
# =====================================================================================

$edit4Old = @'
            var settlements = new Dictionary<long, LineSettlement>();
            foreach (CardTransferProduct line in transfer.Products)
            {
                IReadOnlyList<CardDispositionEntry>? dispositions = line.ProductTransactionWay == ProductTransactionWay.Known
                    ? transfer.Items
                        .Where(i => i.ProductItem.ProductId == line.ProductId)
                        .Select(i => new CardDispositionEntry(i.ProductItemId, TransactionItemReceiveStatus.Disposed))
                        .ToList()
                    : null;

                settlements[line.ProductId] = new LineSettlement(0, line.TransactedQuantity, dispositions);
            }
'@

$edit4New = @'
            var settlements = new Dictionary<long, LineSettlement>();
            foreach (CardTransferProduct line in transfer.Products.Where(p => p.RealQuantityReceived is null))
            {
                IReadOnlyList<CardDispositionEntry>? dispositions = line.ProductTransactionWay == ProductTransactionWay.Known
                    ? transfer.Items
                        .Where(i => i.ProductItem.ProductId == line.ProductId)
                        .Select(i => new CardDispositionEntry(i.ProductItemId, TransactionItemReceiveStatus.Disposed))
                        .ToList()
                    : null;

                settlements[line.ProductId] = new LineSettlement(0, line.TransactedQuantity, dispositions);
            }
'@

Apply-Edit -Path $transferServicePath -Old $edit4Old -New $edit4New `
    -Description '4/6 TransferService.DisposeAsync: skip already-settled lines'

# =====================================================================================
# Edit 5 of 6 - BatchUploadService: narrow the "null BranchID => in transit" re-upload
# guard to Known-way only.
# =====================================================================================

$edit5Old = @'
                    // Transactions §4.10 (T0). A card that has left inventory, or that is committed
                    // to an in-flight transfer, must not be quietly rewritten by a re-upload. The
                    // re-sight below would otherwise reassign BranchID and reset Status to
                    // Available, stranding the transfer's hold quantity with no card behind it.
                    // Rejected per row so the rest of the file still imports, and reported to the
                    // uploader in the failed-rows workbook.
                    if (existingItems.TryGetValue(Convert.ToHexString(fingerprint), out ProductItem? sightedItem))
                    {
                        if (sightedItem.Status == CardStatus.Disposed)
                        {
                            failedRows.Add(new FailedBatchRow(row.RowNumber, maskedPan, FailureReason.CardDisposed));
                            continue;
                        }

                        if (sightedItem.BranchID is null)
                        {
                            failedRows.Add(new FailedBatchRow(row.RowNumber, maskedPan, FailureReason.CardInTransit));
                            continue;
                        }
                    }
'@

$edit5New = @'
                    // Transactions §4.10 (T0). A Known-way card that has left inventory, or that is
                    // committed to an in-flight transfer, must not be quietly rewritten by a
                    // re-upload. The re-sight below would otherwise reassign BranchID and reset
                    // Status to Available, stranding the transfer's hold quantity with no card
                    // behind it. Rejected per row so the rest of the file still imports, and
                    // reported to the uploader in the failed-rows workbook.
                    //
                    // Unknown Inventory Refactor (decisions Q1/Q2): for an Unknown-way product, a
                    // null BranchID is the card's normal resting state now - it sits unassigned in
                    // the tenant-wide pool until printed or disposed by PAN, not "in transit." That
                    // rejection therefore only still applies to Known-way cards.
                    if (existingItems.TryGetValue(Convert.ToHexString(fingerprint), out ProductItem? sightedItem))
                    {
                        if (sightedItem.Status == CardStatus.Disposed)
                        {
                            failedRows.Add(new FailedBatchRow(row.RowNumber, maskedPan, FailureReason.CardDisposed));
                            continue;
                        }

                        if (sightedItem.BranchID is null && product.ProductTransactionWay == ProductTransactionWay.Known)
                        {
                            failedRows.Add(new FailedBatchRow(row.RowNumber, maskedPan, FailureReason.CardInTransit));
                            continue;
                        }
                    }
'@

Apply-Edit -Path $batchUploadServicePath -Old $edit5Old -New $edit5New `
    -Description '5/6 BatchUploadService: narrow in-transit guard to Known-way'

# =====================================================================================
# Edit 6 of 6 - BatchUploadService: stop writing BranchID for Unknown-way ingestion.
# =====================================================================================

$edit6Old = @'
                    foreach ((ParsedBatchRow row, Product product, Branch branch, string maskedPan, byte[] fingerprint) in rowsToProcess)
                    {
                        if (existingItems.TryGetValue(Convert.ToHexString(fingerprint), out ProductItem? existingItem))
                        {
                            // Re-sight (§6.4): update Branch/Status only. BatchId is left as the
                            // batch that first introduced the item — not reassigned here.
                            // BranchID is non-null here: rows whose card is in transit or disposed
                            // were filtered into failedRows during validation above and never
                            // reach rowsToProcess. Pattern-matched rather than null-forgiven so a
                            // future change to that filter breaks loudly instead of silently.
                            if (existingItem.BranchID is long currentBranchId && currentBranchId != branch.Id)
                            {
                                AddDelta(stockDeltas, currentBranchId, existingItem.ProductId, -1);
                                AddDelta(stockDeltas, branch.Id, product.Id, +1);
                                existingItem.BranchID = branch.Id;
                            }

                            existingItem.Status = CardStatus.Available;
                        }
                        else
                        {
                            // PAN Storage Redesign: the full PAN is never persisted in any form.
                            // MaskedPan is display-only; PanFingerprint is the sole identity/dedup
                            // key, computed once per row from the same normalized PAN.
                            var newItem = new ProductItem
                            {
                                PanFingerprint = fingerprint,
                                MaskedPan = maskedPan,
                                TenantId = tenantId,
                                ProductId = product.Id,
                                BranchID = branch.Id,
                                Status = CardStatus.Available,
                                Batch = batch, // relationship fixup populates BatchId on save
                            };
                            newItems.Add(newItem);
                            AddDelta(stockDeltas, branch.Id, product.Id, +1);
                        }

                        importedCount++;
                    }
'@

$edit6New = @'
                    foreach ((ParsedBatchRow row, Product product, Branch branch, string maskedPan, byte[] fingerprint) in rowsToProcess)
                    {
                        bool isUnknownWay = product.ProductTransactionWay == ProductTransactionWay.Unknown;

                        if (existingItems.TryGetValue(Convert.ToHexString(fingerprint), out ProductItem? existingItem))
                        {
                            // Re-sight (§6.4): update Branch/Status only. BatchId is left as the
                            // batch that first introduced the item — not reassigned here.
                            //
                            // Unknown Inventory Refactor (decisions Q1/Q2): an Unknown-way card's
                            // BranchID is never written here - it stays whatever it already was
                            // (null, for every card ingested under the new rule) and this re-sight
                            // only ever refreshes Status. Re-sighting an Unknown-way row therefore
                            // does not move Stock between branches at all; the named branch on the
                            // row is not consulted for an existing Unknown-way card.
                            //
                            // Known-way is unchanged: BranchID is non-null here (rows whose card is
                            // in transit or disposed were filtered into failedRows above and never
                            // reach rowsToProcess), pattern-matched rather than null-forgiven so a
                            // future change to that filter breaks loudly instead of silently.
                            if (!isUnknownWay && existingItem.BranchID is long currentBranchId && currentBranchId != branch.Id)
                            {
                                AddDelta(stockDeltas, currentBranchId, existingItem.ProductId, -1);
                                AddDelta(stockDeltas, branch.Id, product.Id, +1);
                                existingItem.BranchID = branch.Id;
                            }

                            existingItem.Status = CardStatus.Available;
                        }
                        else
                        {
                            // PAN Storage Redesign: the full PAN is never persisted in any form.
                            // MaskedPan is display-only; PanFingerprint is the sole identity/dedup
                            // key, computed once per row from the same normalized PAN.
                            //
                            // Unknown Inventory Refactor (decisions Q1/Q2): BranchID stays null for
                            // a new Unknown-way card - it is only ever pinned to a branch at print
                            // or disposal, keyed by PAN. The row's BranchName still names which
                            // branch's Stock entitlement is credited for the ingestion (decision
                            // Q6), even though it no longer sets the card's physical location.
                            var newItem = new ProductItem
                            {
                                PanFingerprint = fingerprint,
                                MaskedPan = maskedPan,
                                TenantId = tenantId,
                                ProductId = product.Id,
                                BranchID = isUnknownWay ? null : branch.Id,
                                Status = CardStatus.Available,
                                Batch = batch, // relationship fixup populates BatchId on save
                            };
                            newItems.Add(newItem);
                            AddDelta(stockDeltas, branch.Id, product.Id, +1);
                        }

                        importedCount++;
                    }
'@

Apply-Edit -Path $batchUploadServicePath -Old $edit6Old -New $edit6New `
    -Description '6/6 BatchUploadService: BranchID stays null for new Unknown-way cards'

Write-Host ''
Write-Host 'Done. Next steps:'
Write-Host '  1. dotnet build - I could not compile this in the sandbox (no nuget.org egress there); build it locally before trusting it.'
Write-Host '  2. Review TransferService.cs CreateAsync/SettleAsync/DisposeAsync and BatchUploadService.cs against the diff.'
Write-Host '  3. Update Messages_en.resx / Messages_ar.resx if any new failure-reason text is needed for the narrowed CardInTransit guard (none required by this phase - no new error codes were added).'
Write-Host '  4. Still open before the next phase (printing + PAN-based disposal): Q3 (transient print status), Q4 (ProductItem RowVersion), Q5 (retroactive backfill), Q7 (PAN-disposal scope), Q8 (Known-way through print?).'