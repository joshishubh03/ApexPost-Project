using ApexPost.Application.Abstractions;

namespace ApexPost.Tests;

/// <summary>
/// Database-free unit of work for application-service unit tests: runs the use case immediately, serialises
/// concurrent use cases (like a database transaction would for the same rows) and lets nested calls join.
/// </summary>
internal sealed class ImmediateUnitOfWork : IUnitOfWork
{
    public static ImmediateUnitOfWork Instance { get; } = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly AsyncLocal<int> transactionDepth = new();

    private ImmediateUnitOfWork() { }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(0);
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        if (transactionDepth.Value > 0)
        {
            return await operation(cancellationToken);
        }

        await gate.WaitAsync(cancellationToken);
        transactionDepth.Value++;
        try
        {
            return await operation(cancellationToken);
        }
        finally
        {
            transactionDepth.Value--;
            gate.Release();
        }
    }
}
