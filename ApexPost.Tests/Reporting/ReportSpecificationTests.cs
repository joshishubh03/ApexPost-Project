using ApexPost.Application.Models;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Tests.Reporting;

public sealed class ReportSpecificationTests
{
    [Fact]
    public void Settled_transaction_specification_filters_out_open_and_reversed_receipts()
    {
        var clerkId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        var line = new TransactionLine(Guid.NewGuid(), "REPORT-1", "Report item", 1, 10m);
        var paid = CreateTransaction(branchId, clerkId, occurredAt, line, TransactionStatus.Paid);
        var completed = CreateTransaction(branchId, clerkId, occurredAt, line, TransactionStatus.Completed);
        var open = CreateTransaction(branchId, clerkId, occurredAt, line, TransactionStatus.Open);
        var voided = paid.Void(managerId, "Duplicate receipt", occurredAt.AddMinutes(1));

        var selected = ReportFilter.Apply(
            [paid, completed, open, voided],
            SettledTransactionSpecification.Instance);

        Assert.Equal([paid, completed], selected);
    }

    private static CounterTransaction CreateTransaction(
        Guid branchId,
        Guid clerkId,
        DateTimeOffset occurredAt,
        TransactionLine line,
        TransactionStatus status) =>
        new(
            Guid.NewGuid(), $"APT-{Guid.NewGuid():N}", branchId, clerkId,
            occurredAt, [line], null, null, null, null, 0m, PaymentMethod.Cash, status);
}
