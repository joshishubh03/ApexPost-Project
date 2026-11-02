namespace ApexPost.Application.Abstractions;

/// <summary>
/// Coordinates persistence for a single application use case. Repositories only stage changes;
/// the unit of work commits them with one SaveChangesAsync call.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// True when staged repository changes are committed atomically by this unit of work
    /// (EF Core / SQL Server). The JSON adapter writes immediately and returns false.
    /// </summary>
    bool SupportsAtomicCommit => false;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="operation"/> inside one database transaction (with the provider's execution strategy)
    /// and commits the staged changes once at the end. Nested calls join the outer transaction.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
