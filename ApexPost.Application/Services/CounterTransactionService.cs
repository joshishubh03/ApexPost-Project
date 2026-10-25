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
    ICounterTerminalRepository terminalRepository)
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
        var transactions = await transactionRepository.GetAllAsync(cancellationToken);

        return new CounterRegisterContext(
            branch,
            products,
            shippingSchedules,
            terminals,
            transactions.Where(transaction => transaction.BranchId == branchId)
                .OrderByDescending(transaction => transaction.OccurredAt)
                .Take(20)
                .ToArray());
    }

    public async Task<CounterTransaction> CompleteSaleAsync(
        Guid branchId,
        Guid staffId,
        IReadOnlyDictionary<Guid, int> quantities,
        PaymentMethod paymentMethod,
        Guid? shippingRateId,
        decimal? shippingWeightKilograms,
        string? shippingRecipientName,
        string? shippingAddress,
        decimal? parcelLengthCentimeters,
        decimal? parcelWidthCentimeters,
        decimal? parcelHeightCentimeters,
        Guid terminalId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quantities);
        var branch = await branchRepository.GetByIdAsync(branchId, cancellationToken)
            ?? throw new KeyNotFoundException("The branch could not be found.");
        if (!branch.IsOpen)
        {
            throw new InvalidOperationException("A sale cannot be completed at a closed branch.");
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
        var inventory = (await inventoryRepository.GetAllAsync(cancellationToken)).ToList();
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

            if (string.IsNullOrWhiteSpace(shippingRecipientName) || string.IsNullOrWhiteSpace(shippingAddress) ||
                parcelLengthCentimeters is null || parcelWidthCentimeters is null || parcelHeightCentimeters is null)
            {
                throw new InvalidOperationException("Enter the shipment recipient, destination address, and parcel dimensions.");
            }

            shippingQuote = await shippingRateService.QuoteAsync(rateId, shippingWeightKilograms.Value, cancellationToken);
        }
        else if (shippingWeightKilograms is not null || !string.IsNullOrWhiteSpace(shippingRecipientName) ||
                 !string.IsNullOrWhiteSpace(shippingAddress) || parcelLengthCentimeters is not null ||
                 parcelWidthCentimeters is not null || parcelHeightCentimeters is not null)
        {
            throw new InvalidOperationException("Select a shipping service or clear the shipment details.");
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
            terminalId: terminal.Id);

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

        if (inventoryChanges.Count > 0)
        {
            await inventoryRepository.SaveAllAsync(inventory, cancellationToken);
        }

        var transactions = (await transactionRepository.GetAllAsync(cancellationToken)).ToList();
        transactions.Add(transaction);
        await transactionRepository.SaveAllAsync(transactions, cancellationToken);

        await eventPublisher.PublishAsync(new CounterTransactionCompletedEvent(
            transaction.Id,
            transaction.TransactionNumber,
            transaction.BranchId,
            transaction.StaffId,
            transaction.OccurredAt,
            transaction.TotalAmount,
            transaction.PaymentMethod,
            correlationId), CancellationToken.None);

        return transaction;
    }

    public async Task<CounterTransaction?> GetTransactionForBranchAsync(
        Guid branchId,
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var transactions = await transactionRepository.GetAllAsync(cancellationToken);
        return transactions.FirstOrDefault(item => item.Id == transactionId && item.BranchId == branchId);
    }

    public async Task<CounterTransaction> ReverseAsync(
        Guid branchId,
        Guid transactionId,
        Guid managerId,
        bool refund,
        string reason,
        string correlationId,
        CancellationToken cancellationToken = default)
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

        var manager = (await staffRepository.GetAllAsync(cancellationToken))
            .FirstOrDefault(staff => staff.Id == managerId);
        if (manager is null || !manager.IsActive || manager.Role != StaffRole.BranchManager || manager.BranchId != branchId)
        {
            throw new UnauthorizedAccessException("Only an active Branch Manager assigned to this branch can reverse a transaction.");
        }

        var transactions = (await transactionRepository.GetAllAsync(cancellationToken)).ToList();
        var transactionIndex = transactions.FindIndex(item => item.Id == transactionId && item.BranchId == branchId);
        if (transactionIndex < 0)
        {
            throw new KeyNotFoundException("The transaction could not be found for this branch.");
        }

        var now = DateTimeOffset.UtcNow;
        var original = transactions[transactionIndex];
        var reversed = refund
            ? original.Refund(managerId, reason, now)
            : original.Void(managerId, reason, now);

        var inventory = (await inventoryRepository.GetAllAsync(cancellationToken)).ToList();
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
        }

        transactions[transactionIndex] = reversed;
        // Mark the receipt resolved before returning stock so a retry cannot
        // restock it twice. Compensate if the inventory collection write fails.
        await transactionRepository.SaveAllAsync(transactions, CancellationToken.None);
        try
        {
            await inventoryRepository.SaveAllAsync(inventory, CancellationToken.None);
        }
        catch (Exception inventoryException) when (inventoryException is not OperationCanceledException)
        {
            try
            {
                transactions[transactionIndex] = original;
                await transactionRepository.SaveAllAsync(transactions, CancellationToken.None);
            }
            catch (Exception compensationException) when (compensationException is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    "Inventory could not be restored and the transaction status could not be rolled back. Reconcile this receipt before processing it again.",
                    new AggregateException(inventoryException, compensationException));
            }

            throw new InvalidOperationException(
                "Inventory could not be restored. The transaction reversal was rolled back; retry after checking the inventory store.",
                inventoryException);
        }

        await eventPublisher.PublishAsync(new CounterTransactionReversedEvent(
            reversed.Id,
            reversed.TransactionNumber,
            reversed.BranchId,
            managerId,
            now,
            reversed.TotalAmount,
            reversed.Status.ToString(),
            correlationId,
            reversed.ResolutionReason!), CancellationToken.None);

        return reversed;
    }
}
