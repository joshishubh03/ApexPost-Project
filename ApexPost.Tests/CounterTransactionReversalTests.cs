using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Tests;

public sealed class CounterTransactionReversalTests
{
    [Theory]
    [InlineData(false, TransactionStatus.Voided)]
    [InlineData(true, TransactionStatus.Refunded)]
    public void Reversal_records_manager_time_and_reason(bool refund, TransactionStatus expectedStatus)
    {
        var transaction = CreateTransaction();
        var managerId = Guid.NewGuid();
        var resolvedAt = DateTimeOffset.UtcNow;

        var resolved = refund
            ? transaction.Refund(managerId, "Customer request", resolvedAt)
            : transaction.Void(managerId, "Duplicate charge", resolvedAt);

        Assert.Equal(expectedStatus, resolved.Status);
        Assert.Equal(managerId, resolved.ResolvedByStaffId);
        Assert.Equal(resolvedAt, resolved.ResolvedAt);
        Assert.NotNull(resolved.ResolutionReason);
        Assert.Equal(TransactionStatus.Paid, transaction.Status);
        Assert.Equal(transaction.TotalAmount, resolved.TotalAmount);
    }

    [Fact]
    public void Reversal_requires_a_reason()
    {
        var transaction = CreateTransaction();

        Assert.Throws<ArgumentException>(() => transaction.Void(Guid.NewGuid(), " ", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Resolved_transaction_cannot_be_reversed_twice()
    {
        var transaction = CreateTransaction().Void(Guid.NewGuid(), "Duplicate", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => transaction.Refund(Guid.NewGuid(), "Second attempt", DateTimeOffset.UtcNow));
    }

    private static CounterTransaction CreateTransaction() => new(
        Guid.NewGuid(),
        "APT-20261025-12345678",
        Guid.NewGuid(),
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        [new TransactionLine(Guid.NewGuid(), "SKU-1", "Test item", 2, 100m)],
        null,
        null,
        null,
        null,
        0m,
        PaymentMethod.Cash);
}
