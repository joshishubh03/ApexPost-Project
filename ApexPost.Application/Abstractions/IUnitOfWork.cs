namespace ApexPost.Application.Abstractions;

/// <summary>
/// Coordinates persistence for a single application use case. Repositories only stage changes;
/// the unit of work commits them with one SaveChangesAsync call inside one database transaction.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="operation"/> inside one database transaction (with the provider's execution strategy)
    /// and commits the staged changes once at the end. Nested calls join the outer transaction.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Use-case helpers: every application write runs through one of these, so one use case = one commit.
/// </summary>
public static class UnitOfWorkExtensions
{
    public static Task<TResult> ExecuteUseCaseAsync<TResult>(
        this IUnitOfWork unitOfWork,
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(operation);
        return unitOfWork.ExecuteInTransactionAsync(_ => operation(), cancellationToken);
    }

    public static async Task ExecuteUseCaseAsync(
        this IUnitOfWork unitOfWork,
        Func<Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(operation);
        _ = await unitOfWork.ExecuteInTransactionAsync(async _ =>
        {
            await operation();
            return true;
        }, cancellationToken);
    }
}
