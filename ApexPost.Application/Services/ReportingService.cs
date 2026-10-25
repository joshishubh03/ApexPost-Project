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
    private static readonly HashSet<string> ServiceProductCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Postage",
        "Shipping",
        "Money Order"
    };

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
        var moneyOrders = (await moneyOrdersTask).Where(item => item.BranchId == branchId).ToArray();
        var linkedMoneyOrderIds = transactions
            .Where(item => item.MoneyOrderId is not null)
            .Select(item => item.MoneyOrderId!.Value)
            .ToHashSet();
        var standaloneIssuedMoneyOrders = moneyOrders
            .Where(item => item.Status == MoneyOrderStatus.Issued && !linkedMoneyOrderIds.Contains(item.Id))
            .ToArray();
        var dailySalesByDate = completedSales
            .GroupBy(item => DateOnly.FromDateTime(item.OccurredAt.ToLocalTime().DateTime))
            .Select(group =>
            {
                var retailSales = group.Sum(transaction => transaction.Lines
                    .Where(line => !IsServiceProduct(line.ProductId, products))
                    .Sum(line => line.LineTotal + line.TaxAmount));
                var serviceSales = group.Sum(transaction => transaction.Lines
                    .Where(line => IsServiceProduct(line.ProductId, products))
                    .Sum(line => line.LineTotal + line.TaxAmount)
                    + transaction.ShippingCharge + transaction.ShippingTaxAmount
                    + transaction.MoneyOrderAmount + transaction.MoneyOrderFee);
                return new DailySalesSummary(
                    group.Key,
                    group.Count(),
                    0,
                    retailSales,
                    serviceSales,
                    retailSales + serviceSales);
            })
            .ToDictionary(item => item.Date);
        foreach (var group in standaloneIssuedMoneyOrders
            .GroupBy(item => DateOnly.FromDateTime(item.OccurredAt.ToLocalTime().DateTime)))
        {
            var standaloneMoneyOrderTotal = group.Sum(item => item.TotalAmount);
            dailySalesByDate[group.Key] = dailySalesByDate.TryGetValue(group.Key, out var existing)
                ? existing with
                {
                    StandaloneMoneyOrderCount = group.Count(),
                    ServiceSales = existing.ServiceSales + standaloneMoneyOrderTotal,
                    TotalSales = existing.TotalSales + standaloneMoneyOrderTotal
                }
                : new DailySalesSummary(group.Key, 0, group.Count(), 0m, standaloneMoneyOrderTotal, standaloneMoneyOrderTotal);
        }
        var dailySales = dailySalesByDate.Values.OrderByDescending(item => item.Date).ToArray();
        var shippingVolumes = completedSales.Where(item => item.ShippingRateId is not null)
            .GroupBy(item => (Zone: item.ShippingZoneName ?? "Unknown", Level: item.ShippingServiceLevel?.ToString() ?? "Unknown"))
            .Select(group => new ShippingVolumeSummary(group.Key.Zone, group.Key.Level, group.Count()))
            .OrderByDescending(item => item.Shipments)
            .ThenBy(item => item.ZoneName)
            .ToArray();
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
            dailySales,
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

    private static bool IsServiceProduct(Guid productId, IReadOnlyDictionary<Guid, ApexPost.Domain.Entities.Product> products) =>
        products.TryGetValue(productId, out var product) && ServiceProductCategories.Contains(product.Category);
}
