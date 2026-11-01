using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Application.Models;

/// <summary>
/// A reusable predicate for selecting rows that belong in an operational report.
/// </summary>
public interface IReportSpecification<in T>
{
    bool IsSatisfiedBy(T candidate);
}

/// <summary>
/// Applies a report specification to an in-memory result set without coupling report rules to storage.
/// </summary>
public static class ReportFilter
{
    public static IReadOnlyList<T> Apply<T>(
        IEnumerable<T> rows,
        IReportSpecification<T> specification) where T : class
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(specification);
        return rows.Where(specification.IsSatisfiedBy).ToArray();
    }
}

/// <summary>
/// Selects receipts that represent settled sales for sales and shipping reports.
/// </summary>
public sealed class SettledTransactionSpecification : IReportSpecification<CounterTransaction>
{
    public static SettledTransactionSpecification Instance { get; } = new();

    private SettledTransactionSpecification()
    {
    }

    public bool IsSatisfiedBy(CounterTransaction candidate) =>
        candidate.Status is TransactionStatus.Paid or TransactionStatus.Completed;
}
