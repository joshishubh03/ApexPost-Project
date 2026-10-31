using System.Text.Json;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.Repositories;

public sealed class EfInventoryRepository(ApexPostDbContext context) : IInventoryRepository
{
    public async Task<IReadOnlyList<InventoryItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var stockRows = await context.BranchStock.AsNoTracking()
            .OrderBy(row => row.BranchId).ThenBy(row => row.ProductId)
            .ToListAsync(cancellationToken);
        var branchIds = stockRows.Select(row => row.BranchId).Distinct().ToArray();
        var productIds = stockRows.Select(row => row.ProductId).Distinct().ToArray();
        var movements = await context.StockAdjustments.AsNoTracking()
            .Where(row => branchIds.Contains(row.BranchId) && productIds.Contains(row.ProductId))
            .OrderBy(row => row.OccurredAt)
            .ToListAsync(cancellationToken);
        var movementsByStock = movements.GroupBy(row => (row.BranchId, row.ProductId))
            .ToDictionary(group => group.Key, group => (IReadOnlyList<InventoryMovement>)group
                .Select(row => new InventoryMovement(row.StaffId, row.QuantityChange, row.Reason, row.OccurredAt))
                .ToArray());

        return stockRows.Select(row => new InventoryItem(
            row.BranchId,
            row.ProductId,
            row.QuantityOnHand,
            row.ReorderLevel,
            movementsByStock.GetValueOrDefault((row.BranchId, row.ProductId), []))).ToArray();
    }

    public async Task SaveAllAsync(IEnumerable<InventoryItem> items, CancellationToken cancellationToken = default)
    {
        var values = items.ToArray();
        var branchIds = values.Select(value => value.BranchId).Distinct().ToArray();
        var productIds = values.Select(value => value.ProductId).Distinct().ToArray();
        var rows = await context.BranchStock
            .Where(row => branchIds.Contains(row.BranchId) && productIds.Contains(row.ProductId))
            .ToDictionaryAsync(row => (row.BranchId, row.ProductId), cancellationToken);
        var adjustments = await context.StockAdjustments
            .Where(row => branchIds.Contains(row.BranchId) && productIds.Contains(row.ProductId))
            .ToListAsync(cancellationToken);
        var existingMovements = adjustments.Select(ToMovementKey).ToHashSet();

        foreach (var value in values)
        {
            if (rows.TryGetValue((value.BranchId, value.ProductId), out var row))
            {
                if (row.QuantityOnHand != value.QuantityOnHand || row.ReorderLevel != value.ReorderLevel)
                {
                    row.QuantityOnHand = value.QuantityOnHand;
                    row.ReorderLevel = value.ReorderLevel;
                    PersistenceMapping.Touch(row.Audit);
                }
            }
            else
            {
                context.BranchStock.Add(new BranchStockRow
                {
                    BranchId = value.BranchId,
                    ProductId = value.ProductId,
                    QuantityOnHand = value.QuantityOnHand,
                    ReorderLevel = value.ReorderLevel,
                    Audit = PersistenceMapping.NewAudit()
                });
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
                    OccurredAt = movement.OccurredAt,
                    Audit = new AuditColumns
                    {
                        CreatedAtUtc = movement.OccurredAt.UtcDateTime,
                        CreatedByStaffId = movement.StaffId
                    }
                };
                adjustments.Add(adjustment);
                context.StockAdjustments.Add(adjustment);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static (Guid BranchId, Guid ProductId, Guid StaffId, int QuantityChange, string Reason, DateTimeOffset OccurredAt)
        ToMovementKey(StockAdjustmentRow row) =>
        (row.BranchId, row.ProductId, row.StaffId, row.QuantityChange, row.Reason, row.OccurredAt);
}

public sealed class EfIdentityVerificationRepository(ApexPostDbContext context) : IIdentityVerificationRepository
{
    public async Task<IReadOnlyList<IdentityVerificationRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await context.IdentityVerifications.AsNoTracking().OrderBy(row => row.OccurredAt).ToListAsync(cancellationToken);
        return rows.Select(row => new IdentityVerificationRecord(
            row.Id,
            row.BranchId,
            row.StaffId,
            row.OccurredAt,
            row.DocumentType,
            row.MaskedDocumentIdentifier,
            row.MoneyOrderAmount,
            row.Status,
            row.CorrelationId)).ToArray();
    }

    public async Task SaveAllAsync(IEnumerable<IdentityVerificationRecord> records, CancellationToken cancellationToken = default)
    {
        var values = records.ToArray();
        var ids = values.Select(value => value.Id).Distinct().ToArray();
        var existing = await context.IdentityVerifications.Where(row => ids.Contains(row.Id))
            .Select(row => row.Id).ToListAsync(cancellationToken);
        var knownIds = existing.ToHashSet();
        foreach (var record in values.Where(value => !knownIds.Contains(value.Id)))
        {
            context.IdentityVerifications.Add(new IdentityVerificationRow
            {
                Id = record.Id,
                BranchId = record.BranchId,
                StaffId = record.StaffId,
                OccurredAt = record.OccurredAt,
                DocumentType = record.DocumentType,
                MaskedDocumentIdentifier = record.MaskedDocumentIdentifier,
                MoneyOrderAmount = record.MoneyOrderAmount,
                Status = record.Status,
                CorrelationId = record.CorrelationId,
                Audit = new AuditColumns
                {
                    CreatedAtUtc = record.OccurredAt.UtcDateTime,
                    CreatedByStaffId = record.StaffId,
                    CorrelationId = record.CorrelationId
                }
            });
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class EfMoneyOrderRepository(ApexPostDbContext context) : IMoneyOrderRepository
{
    public async Task<IReadOnlyList<MoneyOrder>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await context.MoneyOrders.AsNoTracking().OrderBy(row => row.OccurredAt).ToListAsync(cancellationToken);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task SaveAllAsync(IEnumerable<MoneyOrder> moneyOrders, CancellationToken cancellationToken = default)
    {
        var values = moneyOrders.ToArray();
        var ids = values.Select(value => value.Id).Distinct().ToArray();
        var rows = await context.MoneyOrders.Where(row => ids.Contains(row.Id)).ToDictionaryAsync(row => row.Id, cancellationToken);
        var counterLinks = await context.CounterTransactions.AsNoTracking()
            .Where(row => row.MoneyOrderId != null && ids.Contains(row.MoneyOrderId.Value))
            .ToDictionaryAsync(row => row.MoneyOrderId!.Value, row => row.Id, cancellationToken);

        foreach (var value in values)
        {
            Guid? transactionId = counterLinks.TryGetValue(value.Id, out var linkedTransactionId)
                ? linkedTransactionId
                : null;
            if (rows.TryGetValue(value.Id, out var row))
            {
                if (row.CounterTransactionId == transactionId &&
                    row.BranchId == value.BranchId && row.StaffId == value.StaffId &&
                    row.IdentityVerificationId == value.VerificationRecordId &&
                    row.MoneyOrderNumber == value.MoneyOrderNumber && row.OccurredAt == value.OccurredAt &&
                    row.SenderName == value.SenderName && row.RecipientName == value.RecipientName &&
                    row.Amount == value.Amount && row.Fee == value.Fee &&
                    row.VerificationStatus == value.VerificationStatus && row.DocumentType == value.DocumentType &&
                    row.MaskedDocumentIdentifier == value.MaskedDocumentIdentifier && row.Status == value.Status &&
                    row.RefundedAt == value.RefundedAt && row.RefundedByStaffId == value.RefundedByStaffId &&
                    row.RefundReason == value.RefundReason)
                    continue;

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
                PersistenceMapping.Touch(row.Audit);
            }
            else
            {
                context.MoneyOrders.Add(new MoneyOrderRow
                {
                    Id = value.Id,
                    CounterTransactionId = transactionId,
                    BranchId = value.BranchId,
                    StaffId = value.StaffId,
                    IdentityVerificationId = value.VerificationRecordId,
                    MoneyOrderNumber = value.MoneyOrderNumber,
                    OccurredAt = value.OccurredAt,
                    SenderName = value.SenderName,
                    RecipientName = value.RecipientName,
                    Amount = value.Amount,
                    Fee = value.Fee,
                    VerificationStatus = value.VerificationStatus,
                    DocumentType = value.DocumentType,
                    MaskedDocumentIdentifier = value.MaskedDocumentIdentifier,
                    Status = value.Status,
                    RefundedAt = value.RefundedAt,
                    RefundedByStaffId = value.RefundedByStaffId,
                    RefundReason = value.RefundReason,
                    Audit = new AuditColumns
                    {
                        CreatedAtUtc = value.OccurredAt.UtcDateTime,
                        CreatedByStaffId = value.StaffId
                    }
                });
            }
        }
        await context.SaveChangesAsync(cancellationToken);
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

public sealed class EfCounterTransactionRepository(ApexPostDbContext context) : ICounterTransactionRepository
{
    public async Task<IReadOnlyList<CounterTransaction>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await context.CounterTransactions.AsNoTracking().OrderBy(row => row.OccurredAt).ToListAsync(cancellationToken);
        var ids = rows.Select(row => row.Id).ToArray();
        var lines = await context.TransactionLines.AsNoTracking().Where(row => ids.Contains(row.CounterTransactionId))
            .OrderBy(row => row.CounterTransactionId).ThenBy(row => row.LineNumber).ToListAsync(cancellationToken);
        var payments = await context.Payments.AsNoTracking().Where(row => ids.Contains(row.CounterTransactionId))
            .ToDictionaryAsync(row => row.CounterTransactionId, cancellationToken);
        var linesByTransaction = lines.GroupBy(row => row.CounterTransactionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<TransactionLine>)group.Select(row =>
                new TransactionLine(row.ProductId, row.Sku, row.ProductName, row.Quantity, row.UnitPrice, row.TaxCategory, row.TaxRatePercent))
                .ToArray());

        return rows.Select(row =>
        {
            var paymentMethod = payments.TryGetValue(row.Id, out var payment) ? payment.Method : PaymentMethod.Cash;
            return new CounterTransaction(
                row.Id,
                row.TransactionNumber,
                row.BranchId,
                row.StaffId,
                row.OccurredAt,
                linesByTransaction.GetValueOrDefault(row.Id, []),
                row.ShippingRateId,
                row.ShippingZoneName,
                row.ShippingServiceLevel,
                row.ShippingWeightKilograms,
                row.ShippingCharge,
                paymentMethod,
                row.Status,
                row.ResolvedAt,
                row.ResolvedByStaffId,
                row.ResolutionReason,
                row.ShippingRecipientName,
                row.ShippingAddress,
                row.ParcelLengthCentimeters,
                row.ParcelWidthCentimeters,
                row.ParcelHeightCentimeters,
                row.ShippingTaxRatePercent,
                row.TerminalId,
                row.MoneyOrderId,
                row.MoneyOrderAmount,
                row.MoneyOrderFee,
                row.ShipmentType);
        }).ToArray();
    }

    public async Task SaveAllAsync(IEnumerable<CounterTransaction> transactions, CancellationToken cancellationToken = default)
    {
        var values = transactions.ToArray();
        var ids = values.Select(value => value.Id).Distinct().ToArray();
        var rows = await context.CounterTransactions.Where(row => ids.Contains(row.Id)).ToDictionaryAsync(row => row.Id, cancellationToken);
        var paymentRows = await context.Payments.Where(row => ids.Contains(row.CounterTransactionId))
            .ToDictionaryAsync(row => row.CounterTransactionId, cancellationToken);

        foreach (var value in values)
        {
            if (rows.TryGetValue(value.Id, out var row))
            {
                if (HasChanged(value, row))
                {
                    Apply(value, row);
                    PersistenceMapping.Touch(row.Audit);
                }
            }
            else
            {
                row = new CounterTransactionRow { Id = value.Id, Audit = PersistenceMapping.NewAudit() };
                Apply(value, row);
                context.CounterTransactions.Add(row);
                for (var lineNumber = 0; lineNumber < value.Lines.Count; lineNumber++)
                {
                    var line = value.Lines[lineNumber];
                    context.TransactionLines.Add(new TransactionLineRow
                    {
                        Id = Guid.NewGuid(),
                        CounterTransactionId = value.Id,
                        LineNumber = lineNumber,
                        ProductId = line.ProductId,
                        Sku = line.Sku,
                        ProductName = line.ProductName,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        LineTotal = line.LineTotal,
                        TaxCategory = line.TaxCategory,
                        TaxRatePercent = line.TaxRatePercent,
                        TaxAmount = line.TaxAmount
                    });
                }
            }

            if (paymentRows.TryGetValue(value.Id, out var existingPayment))
            {
                if (existingPayment.Amount != value.TotalAmount || existingPayment.Method != value.PaymentMethod)
                {
                    existingPayment.Amount = value.TotalAmount;
                    existingPayment.Method = value.PaymentMethod;
                    PersistenceMapping.Touch(existingPayment.Audit);
                }
            }
            else
            {
                context.Payments.Add(new PaymentRow
                {
                    Id = Guid.NewGuid(),
                    CounterTransactionId = value.Id,
                    Method = value.PaymentMethod,
                    Amount = value.TotalAmount,
                    PaidAt = value.OccurredAt,
                    Audit = new AuditColumns
                    {
                        CreatedAtUtc = value.OccurredAt.UtcDateTime,
                        CreatedByStaffId = value.StaffId
                    }
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static void Apply(CounterTransaction value, CounterTransactionRow row)
    {
        row.TransactionNumber = value.TransactionNumber;
        row.BranchId = value.BranchId;
        row.StaffId = value.StaffId;
        row.TerminalId = value.TerminalId;
        row.MoneyOrderId = value.MoneyOrderId;
        row.MoneyOrderAmount = value.MoneyOrderAmount;
        row.MoneyOrderFee = value.MoneyOrderFee;
        row.OccurredAt = value.OccurredAt;
        row.ShippingRateId = value.ShippingRateId;
        row.ShippingZoneName = value.ShippingZoneName;
        row.ShippingServiceLevel = value.ShippingServiceLevel;
        row.ShipmentType = value.ShipmentType;
        row.ShippingWeightKilograms = value.ShippingWeightKilograms;
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
        row.ShippingRecipientName = value.ShippingRecipientName;
        row.ShippingAddress = value.ShippingAddress;
        row.ParcelLengthCentimeters = value.ParcelLengthCentimeters;
        row.ParcelWidthCentimeters = value.ParcelWidthCentimeters;
        row.ParcelHeightCentimeters = value.ParcelHeightCentimeters;
    }

    private static bool HasChanged(CounterTransaction value, CounterTransactionRow row) =>
        row.TransactionNumber != value.TransactionNumber || row.BranchId != value.BranchId ||
        row.StaffId != value.StaffId || row.TerminalId != value.TerminalId || row.MoneyOrderId != value.MoneyOrderId ||
        row.MoneyOrderAmount != value.MoneyOrderAmount || row.MoneyOrderFee != value.MoneyOrderFee ||
        row.OccurredAt != value.OccurredAt || row.ShippingRateId != value.ShippingRateId ||
        row.ShippingZoneName != value.ShippingZoneName || row.ShippingServiceLevel != value.ShippingServiceLevel ||
        row.ShipmentType != value.ShipmentType || row.ShippingWeightKilograms != value.ShippingWeightKilograms ||
        row.Subtotal != value.Subtotal || row.TaxTotal != value.TaxTotal || row.ShippingCharge != value.ShippingCharge ||
        row.ShippingTaxRatePercent != value.ShippingTaxRatePercent || row.ShippingTaxAmount != value.ShippingTaxAmount ||
        row.TotalAmount != value.TotalAmount || row.Status != value.Status || row.ResolvedAt != value.ResolvedAt ||
        row.ResolvedByStaffId != value.ResolvedByStaffId || row.ResolutionReason != value.ResolutionReason ||
        row.ShippingRecipientName != value.ShippingRecipientName || row.ShippingAddress != value.ShippingAddress ||
        row.ParcelLengthCentimeters != value.ParcelLengthCentimeters || row.ParcelWidthCentimeters != value.ParcelWidthCentimeters ||
        row.ParcelHeightCentimeters != value.ParcelHeightCentimeters;
}

public sealed class EfAuditLogRepository(ApexPostDbContext context) : IAuditLogRepository
{
    public async Task<IReadOnlyList<AuditRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await context.AuditEntries.AsNoTracking().OrderBy(row => row.OccurredAt).ToListAsync(cancellationToken);
        return rows.Select(row => new AuditRecord(
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
            DeserializeRouteValues(row.RouteValuesJson))).ToArray();
    }

    public async Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        Guid? entityId = Guid.TryParse(record.EntityId, out var parsedEntityId) ? parsedEntityId : null;
        var routeValues = record.RouteValues is null ? null : JsonSerializer.Serialize(record.RouteValues);
        context.AuditEntries.Add(new AuditEntryRow
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
            RouteValuesJson = routeValues,
            SafeDetailsJson = record.Details
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyDictionary<string, string>? DeserializeRouteValues(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(json);
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

    public async Task AppendAsync(OperationalNotification notification, CancellationToken cancellationToken = default)
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
        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class EfApplicationMutationCoordinator(IUnitOfWork unitOfWork) : IApplicationMutationCoordinator
{
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<TResult> RunAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return unitOfWork.ExecuteInTransactionAsync(_ => operation(), cancellationToken);
    }

    public async Task RunAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        _ = await unitOfWork.ExecuteInTransactionAsync(async _ =>
        {
            await operation();
            return true;
        }, cancellationToken);
    }
}
