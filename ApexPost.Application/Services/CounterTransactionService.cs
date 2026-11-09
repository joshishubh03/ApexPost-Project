using ApexPost.Application.Abstractions;
using ApexPost.Application.Events;
using ApexPost.Application.Models;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Application.Services;

public sealed class CounterTransactionService(
    IBranchRepository branchRepository,
    IProductRepository productRepository,
    IInventoryRepository inventoryRepository,
    ICounterTransactionRepository transactionRepository,
    IStaffRepository staffRepository,
    IApplicationEventPublisher eventPublisher,
    InventoryManagementService inventoryManagementService,
    ShippingRateService shippingRateService,
    TaxPolicy taxPolicy,
    ICounterTerminalRepository terminalRepository,
    MoneyOrderService moneyOrderService,
    MoneyOrderPolicy moneyOrderPolicy,
    IUnitOfWork unitOfWork)
{
    public async Task<CounterRegisterContext> GetRegisterContextAsync(
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        var branch = await branchRepository.GetByIdAsync(branchId, cancellationToken)
            ?? throw new KeyNotFoundException("The branch could not be found.");
        var products = await inventoryManagementService.GetBranchInventoryAsync(branchId, cancellationToken);
        var shippingSchedules = await shippingRateService.GetScheduleAsync(includeInactive: false, cancellationToken);
        var terminals = (await terminalRepository.GetAllAsync(cancellationToken))
            .Where(terminal => terminal.BranchId == branchId && terminal.IsActive)
            .OrderBy(terminal => terminal.Code)
            .ToArray();
        var recentTransactions = await transactionRepository.GetRecentForBranchAsync(branchId, 20, cancellationToken);

        return new CounterRegisterContext(
            branch,
            products,
            shippingSchedules,
            terminals,
            recentTransactions);
    }

    public Task<CounterTransaction> CompleteSaleAsync(
        Guid branchId,
        Guid staffId,
        IReadOnlyDictionary<Guid, int> quantities,
        PaymentMethod paymentMethod,
        Guid? shippingRateId,
        ShipmentType? shipmentType,
        decimal? shippingWeightKilograms,
        string? shippingRecipientName,
        string? shippingAddress,
        decimal? parcelLengthCentimeters,
        decimal? parcelWidthCentimeters,
        decimal? parcelHeightCentimeters,
        Guid terminalId,
        decimal? moneyOrderAmount,
        string? moneyOrderSenderName,
        string? moneyOrderRecipientName,
        IdentityDocumentType? moneyOrderDocumentType,
        string? moneyOrderDocumentIdentifier,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        unitOfWork.ExecuteUseCaseAsync(
            () => CompleteSaleCoreAsync(
                branchId, staffId, quantities, paymentMethod, shippingRateId, shipmentType,
                shippingWeightKilograms, shippingRecipientName, shippingAddress,
                parcelLengthCentimeters, parcelWidthCentimeters, parcelHeightCentimeters,
                terminalId, moneyOrderAmount, moneyOrderSenderName, moneyOrderRecipientName,
                moneyOrderDocumentType, moneyOrderDocumentIdentifier, correlationId, cancellationToken),
            cancellationToken);

    private async Task<CounterTransaction> CompleteSaleCoreAsync(
        Guid branchId,
        Guid staffId,
        IReadOnlyDictionary<Guid, int> quantities,
        PaymentMethod paymentMethod,
        Guid? shippingRateId,
        ShipmentType? shipmentType,
        decimal? shippingWeightKilograms,
        string? shippingRecipientName,
        string? shippingAddress,
        decimal? parcelLengthCentimeters,
        decimal? parcelWidthCentimeters,
        decimal? parcelHeightCentimeters,
        Guid terminalId,
        decimal? moneyOrderAmount,
        string? moneyOrderSenderName,
        string? moneyOrderRecipientName,
        IdentityDocumentType? moneyOrderDocumentType,
        string? moneyOrderDocumentIdentifier,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(quantities);
        var branch = await branchRepository.GetByIdAsync(branchId, cancellationToken)
            ?? throw new KeyNotFoundException("The branch could not be found.");
        if (!branch.IsOpen)
        {
            throw new InvalidOperationException("A sale cannot be completed at a closed branch.");
        }

        var staff = await staffRepository.GetByIdAsync(staffId, cancellationToken);
        if (staff is null || !staff.IsActive ||
            staff.Role is not (StaffRole.Administrator or StaffRole.BranchManager or StaffRole.CounterClerk) ||
            (staff.Role != StaffRole.Administrator && staff.BranchId != branchId))
        {
            throw new UnauthorizedAccessException("Only active authorized staff assigned to this branch can complete a sale.");
        }

        var terminal = (await terminalRepository.GetAllAsync(cancellationToken))
            .FirstOrDefault(candidate => candidate.Id == terminalId && candidate.BranchId == branchId && candidate.IsActive)
            ?? throw new InvalidOperationException("Select an active counter terminal assigned to this branch.");

        var requestedQuantities = quantities.Where(pair => pair.Value != 0).ToArray();
        if (requestedQuantities.Any(pair => pair.Value < 0))
        {
            throw new InvalidOperationException("Product quantities must be zero or positive.");
        }

        var products = await productRepository.GetAllAsync(cancellationToken);
        // Only this branch's stock rows are loaded; the repository remembers each row's rowversion
        // so a concurrent sale of the same stock is detected at commit time (optimistic concurrency).
        var originalInventory = (await inventoryRepository.GetForBranchAsync(branchId, cancellationToken)).ToList();
        var inventory = originalInventory.ToList();
        var lines = new List<TransactionLine>();
        var inventoryChanges = new List<(int Index, InventoryItem Updated)>();

        foreach (var requested in requestedQuantities)
        {
            var product = products.FirstOrDefault(candidate => candidate.Id == requested.Key && candidate.IsActive)
                ?? throw new InvalidOperationException("One of the selected products is unavailable.");
            var inventoryIndex = inventory.FindIndex(item => item.BranchId == branchId && item.ProductId == product.Id);
            if (inventoryIndex < 0 || inventory[inventoryIndex].QuantityOnHand < requested.Value)
            {
                throw new InvalidOperationException($"There is not enough stock for {product.Name}.");
            }

            lines.Add(new TransactionLine(
                product.Id,
                product.Sku,
                product.Name,
                requested.Value,
                product.Price,
                product.TaxCategory,
                taxPolicy.GetRatePercent(product.TaxCategory)));
        }

        ShippingQuote? shippingQuote = null;
        if (shippingRateId is { } rateId)
        {
            if (shippingWeightKilograms is null)
            {
                throw new InvalidOperationException("Enter the parcel weight for the selected shipping rate.");
            }

            if (shipmentType is null || string.IsNullOrWhiteSpace(shippingRecipientName) || string.IsNullOrWhiteSpace(shippingAddress))
            {
                throw new InvalidOperationException("Select a shipment type and enter the recipient and destination address.");
            }

            if (shipmentType == ShipmentType.Parcel &&
                (parcelLengthCentimeters is null || parcelWidthCentimeters is null || parcelHeightCentimeters is null))
                throw new InvalidOperationException("Enter all parcel dimensions.");
            if (shipmentType == ShipmentType.Letter &&
                (parcelLengthCentimeters is not null || parcelWidthCentimeters is not null || parcelHeightCentimeters is not null))
                throw new InvalidOperationException("Clear parcel dimensions for a letter shipment.");

            shippingQuote = await shippingRateService.QuoteAsync(rateId, shippingWeightKilograms.Value, cancellationToken);
        }
        else if (shippingWeightKilograms is not null || !string.IsNullOrWhiteSpace(shippingRecipientName) ||
                 shipmentType is not null || !string.IsNullOrWhiteSpace(shippingAddress) || parcelLengthCentimeters is not null ||
                 parcelWidthCentimeters is not null || parcelHeightCentimeters is not null)
        {
            throw new InvalidOperationException("Select a shipping service or clear the shipment details.");
        }

        Guid? moneyOrderId = null;
        decimal moneyOrderFee = 0m;
        if (moneyOrderAmount is { } requestedMoneyOrderAmount)
        {
            if (string.IsNullOrWhiteSpace(moneyOrderSenderName) || string.IsNullOrWhiteSpace(moneyOrderRecipientName))
                throw new InvalidOperationException("Enter the money order sender and recipient.");
            if (moneyOrderPolicy.FeeAmount < 0 || decimal.Round(moneyOrderPolicy.FeeAmount, 2) != moneyOrderPolicy.FeeAmount)
                throw new InvalidOperationException("The configured money order fee is invalid.");
            if (requestedMoneyOrderAmount <= 0 || decimal.Round(requestedMoneyOrderAmount, 2) != requestedMoneyOrderAmount)
                throw new ArgumentOutOfRangeException(nameof(moneyOrderAmount), "Enter a positive money order amount with no more than two decimal places.");
            moneyOrderId = Guid.NewGuid();
            moneyOrderFee = moneyOrderPolicy.FeeAmount;
        }
        else if (!string.IsNullOrWhiteSpace(moneyOrderSenderName) || !string.IsNullOrWhiteSpace(moneyOrderRecipientName) ||
                 moneyOrderDocumentType is not null || !string.IsNullOrWhiteSpace(moneyOrderDocumentIdentifier))
        {
            throw new InvalidOperationException("Enter a money order amount or clear the money order details.");
        }

        var transactionId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        var transactionNumber = $"APT-{occurredAt:yyyyMMdd}-{transactionId.ToString("N")[..8].ToUpperInvariant()}";
        var transaction = new CounterTransaction(
            transactionId,
            transactionNumber,
            branchId,
            staffId,
            occurredAt,
            lines,
            shippingRateId,
            shippingQuote?.ZoneName,
            shippingQuote?.ServiceLevel,
            shippingQuote?.WeightKilograms,
            shippingQuote?.Price ?? 0,
            paymentMethod,
            shippingRecipientName: shippingRecipientName,
            shippingAddress: shippingAddress,
            parcelLengthCentimeters: parcelLengthCentimeters,
            parcelWidthCentimeters: parcelWidthCentimeters,
            parcelHeightCentimeters: parcelHeightCentimeters,
            shippingTaxRatePercent: shippingRateId is null ? 0m : taxPolicy.GetRatePercent(TaxPolicy.ShippingCategory),
            terminalId: terminal.Id,
            moneyOrderId: moneyOrderId,
            moneyOrderAmount: moneyOrderAmount ?? 0m,
            moneyOrderFee: moneyOrderFee,
            shipmentType: shipmentType);

        var adjustmentReason = $"Counter sale {transactionNumber}";
        foreach (var requested in requestedQuantities)
        {
            var productId = requested.Key;
            var index = inventory.FindIndex(item => item.BranchId == branchId && item.ProductId == productId);
            var item = inventory[index];
            inventoryChanges.Add((index, item.ApplyChange(
                -requested.Value,
                item.ReorderLevel,
                adjustmentReason,
                staffId,
                occurredAt)));
        }

        foreach (var change in inventoryChanges)
        {
            inventory[change.Index] = change.Updated;
        }

        var changedItems = inventoryChanges.Select(change => change.Updated).ToArray();
        // Stage the stock decrement, the transaction (lines, payment, shipment) and any money order.
        // Nothing is written yet - the unit of work commits all of it with ONE SaveChangesAsync inside
        // one database transaction when this use case returns.
        await inventoryRepository.UpdateAsync(changedItems, cancellationToken);
        await transactionRepository.AddAsync(transaction, cancellationToken);
        if (moneyOrderId is not null)
        {
            await moneyOrderService.IssueAsync(branchId, staffId,
                moneyOrderSenderName!, moneyOrderRecipientName!, moneyOrderAmount!.Value,
                moneyOrderDocumentType, moneyOrderDocumentIdentifier, correlationId,
                cancellationToken, moneyOrderId);
        }

        await eventPublisher.PublishAsync(new CounterTransactionCompletedEvent(
            transaction.Id,
            transaction.TransactionNumber,
            transaction.BranchId,
            transaction.StaffId,
            transaction.OccurredAt,
            transaction.TotalAmount,
            transaction.PaymentMethod,
            correlationId), CancellationToken.None);

        foreach (var change in inventoryChanges.Where(change =>
            !originalInventory[change.Index].IsBelowReorderLevel && change.Updated.IsBelowReorderLevel))
        {
            var product = products.First(candidate => candidate.Id == change.Updated.ProductId);
            await eventPublisher.PublishAsync(new StockLowEvent(
                branchId, product.Id, product.Name, change.Updated.QuantityOnHand,
                change.Updated.ReorderLevel, staffId, occurredAt, correlationId), CancellationToken.None);
        }

        return transaction;
    }

    public async Task<CounterTransaction?> GetTransactionForBranchAsync(
        Guid branchId,
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var transaction = await transactionRepository.GetByIdAsync(transactionId, cancellationToken);
        return transaction is not null && transaction.BranchId == branchId ? transaction : null;
    }

    public Task<CounterTransaction> ReverseAsync(
        Guid branchId,
        Guid transactionId,
        Guid managerId,
        bool refund,
        string reason,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        unitOfWork.ExecuteUseCaseAsync(
            () => ReverseCoreAsync(branchId, transactionId, managerId, refund, reason, correlationId, cancellationToken),
            cancellationToken);

    private async Task<CounterTransaction> ReverseCoreAsync(
        Guid branchId,
        Guid transactionId,
        Guid managerId,
        bool refund,
        string reason,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Trim().Length > 250)
        {
            throw new ArgumentException("The reason cannot exceed 250 characters.", nameof(reason));
        }

        var branch = await branchRepository.GetByIdAsync(branchId, cancellationToken)
            ?? throw new KeyNotFoundException("The branch could not be found.");
        if (!branch.IsOpen)
        {
            throw new InvalidOperationException("A transaction cannot be reversed at a closed branch.");
        }

        var manager = await staffRepository.GetByIdAsync(managerId, cancellationToken);
        if (manager is null || !manager.IsActive || manager.Role != StaffRole.BranchManager || manager.BranchId != branchId)
        {
            throw new UnauthorizedAccessException("Only an active Branch Manager assigned to this branch can reverse a transaction.");
        }

        var original = await transactionRepository.GetByIdAsync(transactionId, cancellationToken);
        if (original is null || original.BranchId != branchId)
        {
            throw new KeyNotFoundException("The transaction could not be found for this branch.");
        }

        var now = DateTimeOffset.UtcNow;
        if (original.MoneyOrderId is not null && !refund)
            throw new InvalidOperationException("A transaction containing a money order must be refunded so the instrument is canceled.");
        var reversed = refund
            ? original.Refund(managerId, reason, now)
            : original.Void(managerId, reason, now);

        var inventory = (await inventoryRepository.GetForBranchAsync(branchId, cancellationToken)).ToList();
        var restockedProductIds = new HashSet<Guid>();
        foreach (var line in original.Lines)
        {
            var index = inventory.FindIndex(item => item.BranchId == branchId && item.ProductId == line.ProductId);
            if (index < 0)
            {
                throw new InvalidOperationException($"Inventory for {line.ProductName} is missing; the reversal was not saved.");
            }

            inventory[index] = inventory[index].ApplyChange(
                line.Quantity,
                inventory[index].ReorderLevel,
                $"{(refund ? "Refund" : "Void")} {original.TransactionNumber}",
                managerId,
                now);
            restockedProductIds.Add(line.ProductId);
        }

        var restockedItems = inventory.Where(item => restockedProductIds.Contains(item.ProductId)).ToArray();

        if (original.MoneyOrderId is { } moneyOrderId)
        {
            await moneyOrderService.RefundAsync(branchId, moneyOrderId, managerId,
                $"Counter transaction {original.TransactionNumber}: {reason}", correlationId,
                cancellationToken,
                fromCounterTransaction: true);
        }

        // The status change (checked against the CounterTransactions rowversion) and the restock are
        // staged together and committed once by the unit of work.
        await transactionRepository.UpdateAsync(reversed, cancellationToken);
        await inventoryRepository.UpdateAsync(restockedItems, cancellationToken);
        await PublishReversedAsync(reversed, managerId, now, correlationId);
        return reversed;
    }

    private Task PublishReversedAsync(CounterTransaction reversed, Guid managerId, DateTimeOffset now, string correlationId) =>
        eventPublisher.PublishAsync(new CounterTransactionReversedEvent(
            reversed.Id,
            reversed.TransactionNumber,
            reversed.BranchId,
            managerId,
            now,
            reversed.TotalAmount,
            reversed.Status.ToString(),
            correlationId,
            reversed.ResolutionReason!), CancellationToken.None);
}
