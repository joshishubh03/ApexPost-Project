using System.Text.Json;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.Repositories;

/// <summary>
/// Stock repository. Remembers the BranchStock rowversion seen when a row was READ, and uses it as the
/// original value when the row is UPDATED, so a sale committed by another clerk in between raises
/// DbUpdateConcurrencyException (mapped to HTTP 409) instead of overwriting their decrement.
/// </summary>
public sealed class EfInventoryRepository(ApexPostDbContext context) : IInventoryRepository
{
    private readonly Dictionary<(Guid BranchId, Guid ProductId), byte[]> observedVersions = [];

    public Task<IReadOnlyList<InventoryItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        LoadAsync(null, cancellationToken);

    public Task<IReadOnlyList<InventoryItem>> GetForBranchAsync(Guid branchId, CancellationToken cancellationToken = default) =>
        LoadAsync(branchId, cancellationToken);

    private async Task<IReadOnlyList<InventoryItem>> LoadAsync(Guid? branchId, CancellationToken cancellationToken)
    {
        var stockQuery = context.BranchStock.AsNoTracking();
        var movementQuery = context.StockAdjustments.AsNoTracking();
        if (branchId is { } branch)
        {
            stockQuery = stockQuery.Where(row => row.BranchId == branch);
            movementQuery = movementQuery.Where(row => row.BranchId == branch);
        }

        var stockRows = await stockQuery
            .OrderBy(row => row.BranchId).ThenBy(row => row.ProductId)
            .Select(row => new { Row = row, Version = EF.Property<byte[]>(row, AuditShadowProperties.RowVersion) })
            .ToListAsync(cancellationToken);
        var movements = await movementQuery
            .OrderBy(row => row.OccurredAt)
            .Select(row => new { row.BranchId, row.ProductId, row.StaffId, row.QuantityChange, row.Reason, row.OccurredAt })
            .ToListAsync(cancellationToken);
        var movementsByStock = movements.GroupBy(row => (row.BranchId, row.ProductId))
            .ToDictionary(group => group.Key, group => (IReadOnlyList<InventoryMovement>)group
                .Select(row => new InventoryMovement(row.StaffId, row.QuantityChange, row.Reason, row.OccurredAt))
                .ToArray());

        foreach (var stock in stockRows)
        {
            observedVersions[(stock.Row.BranchId, stock.Row.ProductId)] = stock.Version;
        }

        return stockRows.Select(stock => new InventoryItem(
            stock.Row.BranchId,
            stock.Row.ProductId,
            stock.Row.QuantityOnHand,
            stock.Row.ReorderLevel,
            movementsByStock.GetValueOrDefault((stock.Row.BranchId, stock.Row.ProductId), []))).ToArray();
    }

    public Task UpdateAsync(IReadOnlyCollection<InventoryItem> changedItems, CancellationToken cancellationToken = default) =>
        SaveAllAsync(changedItems, cancellationToken);

    /// <summary>Stages an upsert of the given items only (other stock rows are untouched).</summary>
    public async Task SaveAllAsync(IEnumerable<InventoryItem> items, CancellationToken cancellationToken = default)
    {
        var values = items.ToArray();
        if (values.Length == 0)
            return;

        var branchIds = values.Select(value => value.BranchId).Distinct().ToArray();
        var productIds = values.Select(value => value.ProductId).Distinct().ToArray();
        var wanted = values.Select(value => (value.BranchId, value.ProductId)).ToHashSet();
        var rows = (await context.BranchStock
                .Where(row => branchIds.Contains(row.BranchId) && productIds.Contains(row.ProductId))
                .ToListAsync(cancellationToken))
            .ToDictionary(row => (row.BranchId, row.ProductId))
            .WithLocal(context.BranchStock.Local, row => (row.BranchId, row.ProductId), wanted);
        var existingMovements = (await context.StockAdjustments.AsNoTracking()
                .Where(row => branchIds.Contains(row.BranchId) && productIds.Contains(row.ProductId))
                .Select(row => new { row.BranchId, row.ProductId, row.StaffId, row.QuantityChange, row.Reason, row.OccurredAt })
                .ToListAsync(cancellationToken))
            .Select(row => (row.BranchId, row.ProductId, row.StaffId, row.QuantityChange, row.Reason, row.OccurredAt))
            .Concat(context.StockAdjustments.Local.Select(ToMovementKey))
            .ToHashSet();

        foreach (var value in values)
        {
            var key = (value.BranchId, value.ProductId);
            if (rows.TryGetValue(key, out var row))
            {
                if (row.QuantityOnHand != value.QuantityOnHand || row.ReorderLevel != value.ReorderLevel)
                {
                    var entry = context.Entry(row);
                    if (entry.State != EntityState.Added && observedVersions.Remove(key, out var readVersion))
                    {
                        // Concurrency check covers the whole use case: from the read to the commit.
                        entry.Property(AuditShadowProperties.RowVersion).OriginalValue = readVersion;
                    }

                    row.QuantityOnHand = value.QuantityOnHand;
                    row.ReorderLevel = value.ReorderLevel;
                }
            }
            else
            {
                var added = new BranchStockRow
                {
                    BranchId = value.BranchId,
                    ProductId = value.ProductId,
                    QuantityOnHand = value.QuantityOnHand,
                    ReorderLevel = value.ReorderLevel
                };
                context.BranchStock.Add(added);
                rows[key] = added;
            }

            foreach (var movement in value.Movements)
            {
                var movementKey = (value.BranchId, value.ProductId, movement.StaffId, movement.QuantityChange, movement.Reason, movement.OccurredAt);
                if (!existingMovements.Add(movementKey))
                    continue;
                var adjustment = new StockAdjustmentRow
                {
                    Id = Guid.NewGuid(),
                    BranchId = value.BranchId,
                    ProductId = value.ProductId,
                    StaffId = movement.StaffId,
                    QuantityChange = movement.QuantityChange,
                    Reason = movement.Reason,
                    OccurredAt = movement.OccurredAt
                };
                context.StockAdjustments.Add(adjustment);
                context.SetCreatedBy(adjustment, movement.StaffId, movement.OccurredAt);
            }
        }
    }

    private static (Guid BranchId, Guid ProductId, Guid StaffId, int QuantityChange, string Reason, DateTimeOffset OccurredAt)
        ToMovementKey(StockAdjustmentRow row) =>
        (row.BranchId, row.ProductId, row.StaffId, row.QuantityChange, row.Reason, row.OccurredAt);
}

/// <summary>
/// Maps the IdentityVerification aggregate to its separate persistence entity. The row has only a masked,
/// length-limited identifier column; the domain re-validates the mask when a row is read back.
/// </summary>
public sealed class EfIdentityVerificationRepository(ApexPostDbContext context) : IIdentityVerificationRepository
{
    public async Task<IReadOnlyList<IdentityVerificationRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await context.IdentityVerifications.AsNoTracking().OrderBy(row => row.OccurredAt).ToListAsync(cancellationToken);
        return rows.Select(IdentityVerificationMapper.ToDomain).ToArray();
    }

    public Task AddAsync(IdentityVerificationRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        var row = IdentityVerificationMapper.ToRow(record);
        context.IdentityVerifications.Add(row);
        context.SetCreatedBy(row, record.StaffId, record.OccurredAt);
        return Task.CompletedTask;
    }

    public async Task SaveAllAsync(IEnumerable<IdentityVerificationRecord> records, CancellationToken cancellationToken = default)
    {
        var values = records.ToArray();
        var ids = values.Select(value => value.Id).Distinct().ToArray();
        var known = (await context.IdentityVerifications.Where(row => ids.Contains(row.Id))
                .Select(row => row.Id).ToListAsync(cancellationToken))
            .Concat(context.IdentityVerifications.Local.Select(row => row.Id))
            .ToHashSet();
        foreach (var record in values.Where(value => known.Add(value.Id)))
        {
            await AddAsync(record, cancellationToken);
        }
    }
}

/// <summary>Explicit mapper between the IdentityVerificationRecord aggregate and its persistence row.</summary>
public static class IdentityVerificationMapper
{
    public static IdentityVerificationRow ToRow(IdentityVerificationRecord record)
    {
        // Defence in depth: the domain already guarantees this, the CHECK constraint guarantees it again.
        IdentityDocumentMask.EnsureMasked(record.MaskedDocumentIdentifier, nameof(record));
        return new IdentityVerificationRow
        {
            Id = record.Id,
            BranchId = record.BranchId,
            StaffId = record.StaffId,
            OccurredAt = record.OccurredAt,
            DocumentType = record.DocumentType,
            MaskedDocumentIdentifier = record.MaskedDocumentIdentifier,
            MoneyOrderAmount = record.MoneyOrderAmount,
            Status = record.Status,
            CorrelationId = record.CorrelationId
        };
    }

    public static IdentityVerificationRecord ToDomain(IdentityVerificationRow row) => new(
        row.Id,
        row.BranchId,
        row.StaffId,
        row.OccurredAt,
        row.DocumentType,
        row.MaskedDocumentIdentifier,
        row.MoneyOrderAmount,
        row.Status,
        row.CorrelationId);
}

public sealed class EfMoneyOrderRepository(ApexPostDbContext context) : IMoneyOrderRepository
{
    public async Task<IReadOnlyList<MoneyOrder>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await context.MoneyOrders.AsNoTracking().OrderBy(row => row.OccurredAt).ToListAsync(cancellationToken);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task<MoneyOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await context.MoneyOrders.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? context.MoneyOrders.Local.FirstOrDefault(candidate => candidate.Id == id);
        return row is null ? null : ToDomain(row);
    }

    public async Task<IReadOnlyList<MoneyOrder>> GetRecentForBranchAsync(Guid branchId, int take, CancellationToken cancellationToken = default)
    {
        var rows = await context.MoneyOrders.AsNoTracking()
            .Where(row => row.BranchId == branchId)
            .OrderByDescending(row => row.OccurredAt)
            .Take(take)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDomain).ToArray();
    }

    public Task AddAsync(MoneyOrder order, CancellationToken cancellationToken = default) =>
        SaveAllAsync([order], cancellationToken);

    public Task UpdateAsync(MoneyOrder order, CancellationToken cancellationToken = default) =>
        SaveAllAsync([order], cancellationToken);

    public async Task SaveAllAsync(IEnumerable<MoneyOrder> moneyOrders, CancellationToken cancellationToken = default)
    {
        var values = moneyOrders.ToArray();
        var ids = values.Select(value => value.Id).Distinct().ToArray();
        var wanted = ids.ToHashSet();
        var rows = (await context.MoneyOrders.Where(row => ids.Contains(row.Id)).ToDictionaryAsync(row => row.Id, cancellationToken))
            .WithLocal(context.MoneyOrders.Local, row => row.Id, wanted);

        // A money order sold inside a counter basket links back to its (possibly still staged) transaction.
        var counterLinks = (await context.CounterTransactions.AsNoTracking()
                .Where(row => row.MoneyOrderId != null && ids.Contains(row.MoneyOrderId.Value))
                .Select(row => new { MoneyOrderId = row.MoneyOrderId!.Value, row.Id })
                .ToListAsync(cancellationToken))
            .Select(link => (link.MoneyOrderId, link.Id))
            .Concat(context.CounterTransactions.Local
                .Where(row => row.MoneyOrderId is { } linked && wanted.Contains(linked))
                .Select(row => (MoneyOrderId: row.MoneyOrderId!.Value, row.Id)))
            .GroupBy(link => link.MoneyOrderId)
            .ToDictionary(group => group.Key, group => group.First().Id);

        foreach (var value in values)
        {
            Guid? transactionId = counterLinks.TryGetValue(value.Id, out var linkedTransactionId) ? linkedTransactionId : null;
            if (rows.TryGetValue(value.Id, out var row))
            {
                if (Matches(row, value, transactionId))
                    continue;
                Apply(row, value, transactionId);
                if (value.RefundedByStaffId is { } refundedBy)
                {
                    context.SetModifiedBy(row, refundedBy);
                }
            }
            else
            {
                var added = new MoneyOrderRow { Id = value.Id };
                Apply(added, value, transactionId);
                context.MoneyOrders.Add(added);
                context.SetCreatedBy(added, value.StaffId, value.OccurredAt);
                rows[value.Id] = added;
            }
        }
    }

    private static bool Matches(MoneyOrderRow row, MoneyOrder value, Guid? transactionId) =>
        row.CounterTransactionId == transactionId &&
        row.BranchId == value.BranchId && row.StaffId == value.StaffId &&
        row.IdentityVerificationId == value.VerificationRecordId &&
        row.MoneyOrderNumber == value.MoneyOrderNumber && row.OccurredAt == value.OccurredAt &&
        row.SenderName == value.SenderName && row.RecipientName == value.RecipientName &&
        row.Amount == value.Amount && row.Fee == value.Fee &&
        row.VerificationStatus == value.VerificationStatus && row.DocumentType == value.DocumentType &&
        row.MaskedDocumentIdentifier == value.MaskedDocumentIdentifier && row.Status == value.Status &&
        row.RefundedAt == value.RefundedAt && row.RefundedByStaffId == value.RefundedByStaffId &&
        row.RefundReason == value.RefundReason;

    private static void Apply(MoneyOrderRow row, MoneyOrder value, Guid? transactionId)
    {
        row.CounterTransactionId = transactionId;
        row.BranchId = value.BranchId;
        row.StaffId = value.StaffId;
        row.IdentityVerificationId = value.VerificationRecordId;
        row.MoneyOrderNumber = value.MoneyOrderNumber;
        row.OccurredAt = value.OccurredAt;
        row.SenderName = value.SenderName;
        row.RecipientName = value.RecipientName;
        row.Amount = value.Amount;
        row.Fee = value.Fee;
        row.VerificationStatus = value.VerificationStatus;
        row.DocumentType = value.DocumentType;
        row.MaskedDocumentIdentifier = value.MaskedDocumentIdentifier;
        row.Status = value.Status;
        row.RefundedAt = value.RefundedAt;
        row.RefundedByStaffId = value.RefundedByStaffId;
        row.RefundReason = value.RefundReason;
    }

    private static MoneyOrder ToDomain(MoneyOrderRow row) => new(
        row.Id,
        row.MoneyOrderNumber,
        row.BranchId,
        row.StaffId,
        row.OccurredAt,
        row.SenderName,
        row.RecipientName,
        row.Amount,
        row.Fee,
        row.VerificationStatus,
        row.IdentityVerificationId,
        row.DocumentType,
        row.MaskedDocumentIdentifier,
        row.Status,
        row.RefundedAt,
        row.RefundedByStaffId,
        row.RefundReason);
}

/// <summary>
/// Aggregate repository for CounterTransactions + TransactionLines + Payments + Shipments.
/// Reads use Include + AsSplitQuery so the transaction columns are not repeated once per line
/// (no cartesian explosion) and lines are never loaded with one query per transaction (no N+1).
/// </summary>
public sealed class EfCounterTransactionRepository(ApexPostDbContext context) : ICounterTransactionRepository
{
    private readonly Dictionary<Guid, byte[]> observedVersions = [];

    private IQueryable<CounterTransactionRow> AggregateQuery() =>
        context.CounterTransactions.AsNoTracking()
            .Include(row => row.Lines)
            .Include(row => row.Payment)
            .Include(row => row.Shipment)
            .AsSplitQuery();

    public async Task<IReadOnlyList<CounterTransaction>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await AggregateQuery().OrderBy(row => row.OccurredAt).ToListAsync(cancellationToken);
        return await ToDomainAsync(rows, cancellationToken);
    }

    public async Task<CounterTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await AggregateQuery()
            .Where(candidate => candidate.Id == id)
            .OrderBy(candidate => candidate.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
            return null;

        var version = await context.CounterTransactions.AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => EF.Property<byte[]>(candidate, AuditShadowProperties.RowVersion))
            .SingleAsync(cancellationToken);
        observedVersions[id] = version;
        return (await ToDomainAsync([row], cancellationToken))[0];
    }

    public async Task<IReadOnlyList<CounterTransaction>> GetRecentForBranchAsync(Guid branchId, int take, CancellationToken cancellationToken = default)
    {
        var rows = await AggregateQuery()
            .Where(row => row.BranchId == branchId)
            .OrderByDescending(row => row.OccurredAt)
            .Take(take)
            .ToListAsync(cancellationToken);
        return await ToDomainAsync(rows, cancellationToken);
    }

    public Task AddAsync(CounterTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var row = new CounterTransactionRow { Id = transaction.Id };
        ApplyHeader(transaction, row);
        for (var lineNumber = 0; lineNumber < transaction.Lines.Count; lineNumber++)
        {
            var line = transaction.Lines[lineNumber];
            row.Lines.Add(new TransactionLineRow
            {
                Id = Guid.NewGuid(),
                CounterTransactionId = transaction.Id,
                LineNumber = lineNumber,
                ProductId = line.ProductId,
                Sku = new Sku(line.Sku),
                ProductName = line.ProductName,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                LineTotal = line.LineTotal,
                TaxCategory = line.TaxCategory,
                TaxRatePercent = line.TaxRatePercent,
                TaxAmount = line.TaxAmount
            });
        }

        if (transaction.TotalAmount > 0)
        {
            row.Payment = new PaymentRow
            {
                Id = Guid.NewGuid(),
                CounterTransactionId = transaction.Id,
                Method = transaction.PaymentMethod,
                Amount = transaction.TotalAmount,
                PaidAt = transaction.OccurredAt
            };
        }

        if (transaction.ShippingRateId is { } rateId)
        {
            row.Shipment = new ShipmentRow
            {
                Id = Guid.NewGuid(),
                CounterTransactionId = transaction.Id,
                ShippingRateId = rateId,
                ShipmentType = transaction.ShipmentType,
                Weight = new Weight(transaction.ShippingWeightKilograms!.Value),
                Dimensions = transaction.ParcelLengthCentimeters is { } length
                    ? new ParcelDimensions
                    {
                        LengthCentimeters = length,
                        WidthCentimeters = transaction.ParcelWidthCentimeters!.Value,
                        HeightCentimeters = transaction.ParcelHeightCentimeters!.Value
                    }
                    : null,
                Destination = transaction.ShippingRecipientName is { } recipient
                    ? new ShippingAddress { RecipientName = recipient, AddressLine = transaction.ShippingAddress ?? string.Empty }
                    : null,
                Status = ShipmentStatusFor(transaction.Status)
            };
        }

        // One Add stages the whole graph (transaction, lines, payment, shipment).
        context.CounterTransactions.Add(row);
        context.SetCreatedBy(row, transaction.StaffId, transaction.OccurredAt);
        if (row.Payment is not null)
            context.SetCreatedBy(row.Payment, transaction.StaffId, transaction.OccurredAt);
        if (row.Shipment is not null)
            context.SetCreatedBy(row.Shipment, transaction.StaffId, transaction.OccurredAt);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(CounterTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var row = context.CounterTransactions.Local.FirstOrDefault(candidate => candidate.Id == transaction.Id)
            ?? await context.CounterTransactions
                .Include(candidate => candidate.Shipment)
                .SingleOrDefaultAsync(candidate => candidate.Id == transaction.Id, cancellationToken)
            ?? throw new KeyNotFoundException("The transaction could not be found.");

        var entry = context.Entry(row);
        if (entry.State != EntityState.Added && observedVersions.Remove(transaction.Id, out var readVersion))
        {
            entry.Property(AuditShadowProperties.RowVersion).OriginalValue = readVersion;
        }

        if (!HeaderChanged(transaction, row))
            return;

        ApplyHeader(transaction, row);
        if (row.Shipment is not null)
        {
            row.Shipment.Status = ShipmentStatusFor(transaction.Status);
        }

        if (transaction.ResolvedByStaffId is { } resolvedBy)
        {
            context.SetModifiedBy(row, resolvedBy);
        }
    }

    /// <summary>Bulk upsert used by the demo-data seeder. Stages only rows that are new or changed.</summary>
    public async Task SaveAllAsync(IEnumerable<CounterTransaction> transactions, CancellationToken cancellationToken = default)
    {
        var values = transactions.ToArray();
        var ids = values.Select(value => value.Id).Distinct().ToArray();
        var existing = (await context.CounterTransactions.AsNoTracking()
                .Where(row => ids.Contains(row.Id))
                .Select(row => new { row.Id, row.Status, row.ResolvedAt })
                .ToListAsync(cancellationToken))
            .ToDictionary(row => row.Id);
        var staged = context.CounterTransactions.Local.Select(row => row.Id).ToHashSet();

        foreach (var value in values)
        {
            if (existing.TryGetValue(value.Id, out var current))
            {
                if (current.Status != value.Status || current.ResolvedAt != value.ResolvedAt)
                {
                    await UpdateAsync(value, cancellationToken);
                }
            }
            else if (!staged.Contains(value.Id))
            {
                await AddAsync(value, cancellationToken);
            }
        }
    }

    private async Task<IReadOnlyList<CounterTransaction>> ToDomainAsync(
        IReadOnlyCollection<CounterTransactionRow> rows,
        CancellationToken cancellationToken)
    {
        // One extra query for the shipping lookup (zone name and service level) - not one per transaction.
        var rateIds = rows.Where(row => row.Shipment is not null).Select(row => row.Shipment!.ShippingRateId).Distinct().ToArray();
        var rates = rateIds.Length == 0
            ? new Dictionary<Guid, (string ZoneName, ShippingServiceLevel ServiceLevel)>()
            : (await context.ShippingRates.AsNoTracking()
                    .Where(rate => rateIds.Contains(rate.Id))
                    .Join(context.ShippingZones.AsNoTracking(), rate => rate.ZoneId, zone => zone.Id,
                        (rate, zone) => new { rate.Id, ZoneName = zone.Name, rate.ServiceLevel })
                    .ToListAsync(cancellationToken))
                .ToDictionary(rate => rate.Id, rate => (rate.ZoneName, rate.ServiceLevel));

        return rows.Select(row =>
        {
            var shipment = row.Shipment;
            (string ZoneName, ShippingServiceLevel ServiceLevel)? rate =
                shipment is not null && rates.TryGetValue(shipment.ShippingRateId, out var found) ? found : null;
            return new CounterTransaction(
                row.Id,
                row.TransactionNumber,
                row.BranchId,
                row.StaffId,
                row.OccurredAt,
                row.Lines.OrderBy(line => line.LineNumber)
                    .Select(line => new TransactionLine(line.ProductId, line.Sku.Value, line.ProductName, line.Quantity,
                        line.UnitPrice, line.TaxCategory, line.TaxRatePercent))
                    .ToArray(),
                shipment?.ShippingRateId,
                rate?.ZoneName,
                rate?.ServiceLevel,
                shipment?.Weight.Kilograms,
                row.ShippingCharge,
                row.Payment?.Method ?? PaymentMethod.Cash,
                row.Status,
                row.ResolvedAt,
                row.ResolvedByStaffId,
                row.ResolutionReason,
                shipment?.Destination?.RecipientName,
                shipment?.Destination?.AddressLine,
                shipment?.Dimensions?.LengthCentimeters,
                shipment?.Dimensions?.WidthCentimeters,
                shipment?.Dimensions?.HeightCentimeters,
                row.ShippingTaxRatePercent,
                row.TerminalId,
                row.MoneyOrderId,
                row.MoneyOrderAmount,
                row.MoneyOrderFee,
                shipment?.ShipmentType);
        }).ToArray();
    }

    private static ShipmentStatus ShipmentStatusFor(TransactionStatus status) =>
        status is TransactionStatus.Voided or TransactionStatus.Refunded ? ShipmentStatus.Cancelled : ShipmentStatus.Booked;

    private static void ApplyHeader(CounterTransaction value, CounterTransactionRow row)
    {
        row.TransactionNumber = value.TransactionNumber;
        row.BranchId = value.BranchId;
        row.StaffId = value.StaffId;
        row.TerminalId = value.TerminalId;
        row.MoneyOrderId = value.MoneyOrderId;
        row.MoneyOrderAmount = value.MoneyOrderAmount;
        row.MoneyOrderFee = value.MoneyOrderFee;
        row.OccurredAt = value.OccurredAt;
        row.Subtotal = value.Subtotal;
        row.TaxTotal = value.TaxTotal;
        row.ShippingCharge = value.ShippingCharge;
        row.ShippingTaxRatePercent = value.ShippingTaxRatePercent;
        row.ShippingTaxAmount = value.ShippingTaxAmount;
        row.TotalAmount = value.TotalAmount;
        row.Status = value.Status;
        row.ResolvedAt = value.ResolvedAt;
        row.ResolvedByStaffId = value.ResolvedByStaffId;
        row.ResolutionReason = value.ResolutionReason;
    }

    private static bool HeaderChanged(CounterTransaction value, CounterTransactionRow row) =>
        row.Status != value.Status || row.ResolvedAt != value.ResolvedAt ||
        row.ResolvedByStaffId != value.ResolvedByStaffId || row.ResolutionReason != value.ResolutionReason ||
        row.TotalAmount != value.TotalAmount || row.MoneyOrderId != value.MoneyOrderId;
}

/// <summary>Domain-event audit records. Staged on the business unit of work so they commit atomically with it.</summary>
public sealed class EfAuditLogRepository(ApexPostDbContext context) : IAuditLogRepository
{
    public async Task<IReadOnlyList<AuditRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await context.AuditEntries.AsNoTracking().OrderBy(row => row.OccurredAt).ToListAsync(cancellationToken);
        return rows.Select(AuditEntryMapper.ToRecord).ToArray();
    }

    public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        context.AuditEntries.Add(AuditEntryMapper.ToRow(record));
        return Task.CompletedTask;
    }
}

internal static class AuditEntryMapper
{
    public static AuditEntryRow ToRow(AuditRecord record)
    {
        Guid? entityId = Guid.TryParse(record.EntityId, out var parsedEntityId) ? parsedEntityId : null;
        return new AuditEntryRow
        {
            Id = record.Id,
            StaffId = record.ActorId == Guid.Empty ? null : record.ActorId,
            BranchId = record.BranchId == Guid.Empty ? null : record.BranchId,
            Role = record.Role ?? string.Empty,
            Action = record.Action,
            EntityType = record.EntityType,
            EntityId = entityId,
            ResultStatusCode = record.ResultStatusCode ?? 200,
            OccurredAt = record.OccurredAt,
            CorrelationId = record.CorrelationId,
            Amount = record.Amount,
            DurationMilliseconds = record.DurationMilliseconds,
            RouteValuesJson = record.RouteValues is null ? null : JsonSerializer.Serialize(record.RouteValues),
            SafeDetailsJson = record.Details
        };
    }

    public static AuditRecord ToRecord(AuditEntryRow row) => new(
        row.Id,
        row.OccurredAt,
        row.Action,
        row.EntityType,
        row.EntityId?.ToString("D") ?? string.Empty,
        row.StaffId ?? Guid.Empty,
        row.BranchId ?? Guid.Empty,
        row.CorrelationId,
        row.Amount,
        row.SafeDetailsJson,
        row.Role,
        row.ResultStatusCode,
        row.DurationMilliseconds,
        string.IsNullOrWhiteSpace(row.RouteValuesJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(row.RouteValuesJson));
}

public sealed class EfNotificationRepository(ApexPostDbContext context) : INotificationRepository
{
    public async Task<IReadOnlyList<OperationalNotification>> GetForBranchAsync(
        Guid branchId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(limit));
        var rows = await context.OperationalNotifications.AsNoTracking()
            .Where(row => row.BranchId == branchId)
            .OrderByDescending(row => row.OccurredAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return rows.Select(row => new OperationalNotification(
            row.Id,
            row.Kind,
            row.Message,
            row.BranchId ?? Guid.Empty,
            row.StaffId ?? Guid.Empty,
            row.OccurredAt,
            row.CorrelationId,
            row.ReferenceId)).ToArray();
    }

    public Task AppendAsync(OperationalNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        context.OperationalNotifications.Add(new OperationalNotificationRow
        {
            Id = notification.Id,
            BranchId = notification.BranchId,
            StaffId = notification.ActorId,
            Kind = notification.Type,
            Message = notification.Message,
            OccurredAt = notification.OccurredAt,
            CorrelationId = notification.CorrelationId,
            ReferenceId = notification.ReferenceId,
            IsAcknowledged = false
        });
        return Task.CompletedTask;
    }
}
