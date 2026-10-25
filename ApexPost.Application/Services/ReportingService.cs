using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Domain.Enums;

namespace ApexPost.Application.Services;

public sealed class ReportingService(
    IBranchRepository branchRepository,
    ICounterTransactionRepository transactionRepository,
    IInventoryRepository inventoryRepository,
    IProductRepository productRepository,
    IMoneyOrderRepository moneyOrderRepository,
    IIdentityVerificationRepository verificationRepository,
    IAuditLogRepository auditLogRepository,
    INotificationRepository notificationRepository,
    LowStockRule lowStockRule)
{
    public async Task<BranchOperationsReport> GetBranchReportAsync(
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        var branchTask = branchRepository.GetByIdAsync(branchId, cancellationToken);
        var transactionsTask = transactionRepository.GetAllAsync(cancellationToken);
        var inventoryTask = inventoryRepository.GetAllAsync(cancellationToken);
        var productsTask = productRepository.GetAllAsync(cancellationToken);
        var moneyOrdersTask = moneyOrderRepository.GetAllAsync(cancellationToken);
        var verificationsTask = verificationRepository.GetAllAsync(cancellationToken);
        var auditTask = auditLogRepository.GetAllAsync(cancellationToken);
        var notificationsTask = notificationRepository.GetForBranchAsync(branchId, 20, cancellationToken);
        await Task.WhenAll(branchTask, transactionsTask, inventoryTask, productsTask, moneyOrdersTask, verificationsTask, auditTask, notificationsTask);

        var branch = await branchTask ?? throw new KeyNotFoundException("The branch could not be found.");
        var transactions = (await transactionsTask).Where(item => item.BranchId == branchId).ToArray();
        var products = (await productsTask).ToDictionary(product => product.Id);
        var inventory = (await inventoryTask).Where(item => item.BranchId == branchId).ToArray();
        var lowStock = inventory.Where(item => lowStockRule.IsLowStock(item.QuantityOnHand, item.ReorderLevel))
            .Select(item => products.TryGetValue(item.ProductId, out var product) && product.IsActive
                ? new ProductInventorySummary(
                    product.Id, product.Sku, product.Name, product.Category, product.TaxCategory, product.Price,
                    item.QuantityOnHand, item.ReorderLevel, true, true,
                    item.Movements.OrderByDescending(movement => movement.OccurredAt).Take(3).ToArray())
                : null)
            .Where(item => item is not null)
            .Cast<ProductInventorySummary>()
            .OrderBy(item => item.QuantityOnHand)
            .ThenBy(item => item.Name)
            .ToArray();
        var completedSales = transactions.Where(item => item.Status == TransactionStatus.Completed).ToArray();
        var shippingVolumes = completedSales.Where(item => item.ShippingRateId is not null)
            .GroupBy(item => (Zone: item.ShippingZoneName ?? "Unknown", Level: item.ShippingServiceLevel?.ToString() ?? "Unknown"))
            .Select(group => new ShippingVolumeSummary(group.Key.Zone, group.Key.Level, group.Count()))
            .OrderByDescending(item => item.Shipments)
            .ThenBy(item => item.ZoneName)
            .ToArray();
        var moneyOrders = (await moneyOrdersTask).Where(item => item.BranchId == branchId).ToArray();
        var issuedMoneyOrders = moneyOrders.Where(item => item.Status == ApexPost.Domain.Enums.MoneyOrderStatus.Issued).ToArray();
        var failures = (await verificationsTask).Count(item =>
            item.BranchId == branchId && item.Status == IdentityVerificationStatus.Failed);
        var recentAudit = (await auditTask).Where(item => item.BranchId == branchId)
            .OrderByDescending(item => item.OccurredAt)
            .Take(50)
            .ToArray();

        return new BranchOperationsReport(
            branch,
            completedSales.Length,
            completedSales.Sum(item => item.TotalAmount),
            transactions.Count(item => item.Status == TransactionStatus.Voided),
            transactions.Count(item => item.Status == TransactionStatus.Refunded),
            lowStock,
            shippingVolumes,
            issuedMoneyOrders.Length,
            issuedMoneyOrders.Sum(item => item.TotalAmount),
            moneyOrders.Count(item => item.Status == ApexPost.Domain.Enums.MoneyOrderStatus.Refunded),
            failures,
            await notificationsTask,
            recentAudit);
    }
}
