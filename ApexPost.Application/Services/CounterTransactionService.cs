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
    IApplicationEventPublisher eventPublisher,
    InventoryManagementService inventoryManagementService,
    ShippingRateService shippingRateService)
{
    public async Task<CounterRegisterContext> GetRegisterContextAsync(
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        var branch = await branchRepository.GetByIdAsync(branchId, cancellationToken)
            ?? throw new KeyNotFoundException("The branch could not be found.");
        var products = await inventoryManagementService.GetBranchInventoryAsync(branchId, cancellationToken);
        var shippingSchedules = await shippingRateService.GetScheduleAsync(includeInactive: false, cancellationToken);
        var transactions = await transactionRepository.GetAllAsync(cancellationToken);

        return new CounterRegisterContext(
            branch,
            products,
            shippingSchedules,
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

            lines.Add(new TransactionLine(product.Id, product.Sku, product.Name, requested.Value, product.Price));
        }

        ShippingQuote? shippingQuote = null;
        if (shippingRateId is { } rateId)
        {
            if (shippingWeightKilograms is null)
            {
                throw new InvalidOperationException("Enter the parcel weight for the selected shipping rate.");
            }

            shippingQuote = await shippingRateService.QuoteAsync(rateId, shippingWeightKilograms.Value, cancellationToken);
        }
        else if (shippingWeightKilograms is not null)
        {
            throw new InvalidOperationException("Select a shipping service or clear the parcel weight.");
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
            paymentMethod);

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
}
