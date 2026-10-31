using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Application.Services;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Tests.Reporting;

public sealed class ReportingServiceTests
{
    [Fact]
    public async Task Branch_report_aggregates_only_branch_sales_and_avoids_linked_money_order_duplicates()
    {
        var branchId = Guid.NewGuid();
        var otherBranchId = Guid.NewGuid();
        var clerkId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var branch = new Branch(branchId, "AP-001", "Central", "North", true);
        var retailProduct = new Product(Guid.NewGuid(), "RETAIL-1", "Notebook", "Paper notebook", "Stationery", 10m, true);
        var postageProduct = new Product(Guid.NewGuid(), "POST-1", "Postage", "Letter postage", "Postage", 5m, true);
        var inactiveProduct = new Product(Guid.NewGuid(), "OLD-1", "Inactive stock", "Discontinued", "Stationery", 2m, false);
        var retailLine = new TransactionLine(retailProduct.Id, retailProduct.Sku, retailProduct.Name, 2, 10m, "Standard", 5m);
        var postageLine = new TransactionLine(postageProduct.Id, postageProduct.Sku, postageProduct.Name, 1, 5m, "Standard", 10m);
        var linkedMoneyOrder = CreateMoneyOrder(Guid.NewGuid(), branchId, clerkId, occurredAt, 50m, 3m);
        var standaloneMoneyOrder = CreateMoneyOrder(Guid.NewGuid(), branchId, clerkId, occurredAt, 20m, 2m);
        var refundedMoneyOrder = CreateMoneyOrder(
            Guid.NewGuid(), branchId, clerkId, occurredAt, 30m, 3m,
            MoneyOrderStatus.Refunded, managerId, "Customer cancellation");
        var completedTransaction = new CounterTransaction(
            Guid.NewGuid(), "APT-20260615-REPORT001", branchId, clerkId, occurredAt,
            [retailLine, postageLine], Guid.NewGuid(), "Local", ShippingServiceLevel.Express, 0.5m, 7m,
            PaymentMethod.Cash,
            shippingTaxRatePercent: 5m,
            moneyOrderId: linkedMoneyOrder.Id,
            moneyOrderAmount: linkedMoneyOrder.Amount,
            moneyOrderFee: linkedMoneyOrder.Fee);
        var voidedTransaction = completedTransaction.Void(managerId, "Duplicate receipt", occurredAt.AddHours(1));
        var refundedTransaction = new CounterTransaction(
            Guid.NewGuid(), "APT-20260615-REPORT002", branchId, clerkId, occurredAt,
            [retailLine], null, null, null, null, 0m, PaymentMethod.Card)
            .Refund(managerId, "Customer return", occurredAt.AddHours(2));
        var otherBranchTransaction = new CounterTransaction(
            Guid.NewGuid(), "APT-20260615-OTHER001", otherBranchId, clerkId, occurredAt,
            [retailLine], null, null, null, null, 0m, PaymentMethod.Cash);
        var failedVerification = new IdentityVerificationRecord(
            Guid.NewGuid(), branchId, clerkId, occurredAt, IdentityDocumentType.Passport,
            "*****1234", 10_001m, IdentityVerificationStatus.Failed, "report-correlation");
        var otherBranchVerification = new IdentityVerificationRecord(
            Guid.NewGuid(), otherBranchId, clerkId, occurredAt, IdentityDocumentType.Passport,
            "*****5678", 10_001m, IdentityVerificationStatus.Failed, "other-correlation");
        var notification = new OperationalNotification(
            Guid.NewGuid(), "StockLow", "Stock reached its reorder level.", branchId, clerkId,
            occurredAt, "report-correlation", retailProduct.Id.ToString("D"));

        var service = new ReportingService(
            new FakeBranchRepository(branch),
            new FakeCounterTransactionRepository([completedTransaction, voidedTransaction, refundedTransaction, otherBranchTransaction]),
            new FakeInventoryRepository([
                new InventoryItem(branchId, retailProduct.Id, 2, 5, []),
                new InventoryItem(branchId, inactiveProduct.Id, 0, 1, []),
                new InventoryItem(otherBranchId, retailProduct.Id, 0, 5, [])
            ]),
            new FakeProductRepository([retailProduct, postageProduct, inactiveProduct]),
            new FakeMoneyOrderRepository([linkedMoneyOrder, standaloneMoneyOrder, refundedMoneyOrder]),
            new FakeVerificationRepository([failedVerification, otherBranchVerification]),
            new FakeAuditLogRepository([]),
            new FakeNotificationRepository([notification]),
            LowStockRule.AtOrBelowReorderLevel);

        var report = await service.GetBranchReportAsync(branchId);

        Assert.Equal(1, report.CompletedSalesCount);
        Assert.Equal(completedTransaction.TotalAmount, report.CompletedSalesTotal);
        Assert.Equal(1, report.VoidedTransactionsCount);
        Assert.Equal(1, report.RefundedTransactionsCount);
        Assert.Equal(retailProduct.Id, Assert.Single(report.LowStockItems).ProductId);
        var shippingVolume = Assert.Single(report.ShippingVolumes);
        Assert.Equal("Local", shippingVolume.ZoneName);
        Assert.Equal(nameof(ShippingServiceLevel.Express), shippingVolume.ServiceLevel);
        Assert.Equal(1, shippingVolume.Shipments);
        Assert.Equal(2, report.MoneyOrderCount);
        Assert.Equal(75m, report.MoneyOrderTotal);
        Assert.Equal(1, report.RefundedMoneyOrderCount);
        Assert.Equal(1, report.IdentityVerificationFailures);
        Assert.Equal(notification, Assert.Single(report.RecentNotifications));

        var dailySales = Assert.Single(report.DailySales);
        Assert.Equal(new DateOnly(2026, 6, 15), dailySales.Date);
        Assert.Equal(1, dailySales.CompletedTransactionCount);
        Assert.Equal(1, dailySales.StandaloneMoneyOrderCount);
        Assert.Equal(21m, dailySales.RetailSales);
        Assert.Equal(87.85m, dailySales.ServiceSales);
        Assert.Equal(108.85m, dailySales.TotalSales);
    }

    private static MoneyOrder CreateMoneyOrder(
        Guid id,
        Guid branchId,
        Guid staffId,
        DateTimeOffset occurredAt,
        decimal amount,
        decimal fee,
        MoneyOrderStatus status = MoneyOrderStatus.Issued,
        Guid? refundedBy = null,
        string? refundReason = null) =>
        new(
            id,
            $"APM-{id:N}",
            branchId,
            staffId,
            occurredAt,
            "Sender",
            "Recipient",
            amount,
            fee,
            IdentityVerificationStatus.NotRequired,
            null,
            null,
            null,
            status,
            status == MoneyOrderStatus.Refunded ? occurredAt.AddHours(1) : null,
            refundedBy,
            refundReason);

    private sealed class FakeBranchRepository(Branch branch) : IBranchRepository
    {
        public Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Branch>>([branch]);

        public Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Branch?>(id == branch.Id ? branch : null);

        public Task SaveAllAsync(IEnumerable<Branch> branches, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeCounterTransactionRepository(IReadOnlyList<CounterTransaction> transactions)
        : ICounterTransactionRepository
    {
        public Task<IReadOnlyList<CounterTransaction>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(transactions);

        public Task SaveAllAsync(IEnumerable<CounterTransaction> transactions, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeInventoryRepository(IReadOnlyList<InventoryItem> inventory) : IInventoryRepository
    {
        public Task<IReadOnlyList<InventoryItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(inventory);

        public Task SaveAllAsync(IEnumerable<InventoryItem> items, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeProductRepository(IReadOnlyList<Product> products) : IProductRepository
    {
        public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(products);

        public Task SaveAllAsync(IEnumerable<Product> items, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public ValueTask<string> GetVersionAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult("0");
    }

    private sealed class FakeMoneyOrderRepository(IReadOnlyList<MoneyOrder> moneyOrders) : IMoneyOrderRepository
    {
        public Task<IReadOnlyList<MoneyOrder>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(moneyOrders);

        public Task SaveAllAsync(IEnumerable<MoneyOrder> items, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeVerificationRepository(IReadOnlyList<IdentityVerificationRecord> records)
        : IIdentityVerificationRepository
    {
        public Task<IReadOnlyList<IdentityVerificationRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(records);

        public Task SaveAllAsync(IEnumerable<IdentityVerificationRecord> items, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeAuditLogRepository(IReadOnlyList<AuditRecord> records) : IAuditLogRepository
    {
        public Task<IReadOnlyList<AuditRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(records);

        public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeNotificationRepository(IReadOnlyList<OperationalNotification> notifications)
        : INotificationRepository
    {
        public Task<IReadOnlyList<OperationalNotification>> GetForBranchAsync(
            Guid branchId,
            int limit = 20,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OperationalNotification>>(
                notifications.Where(item => item.BranchId == branchId).Take(limit).ToArray());

        public Task AppendAsync(OperationalNotification notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
