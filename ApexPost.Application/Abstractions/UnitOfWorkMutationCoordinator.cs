namespace ApexPost.Application.Abstractions;

/// <summary>
/// Use-case boundary for the SQL Server adapter: every application write runs inside one
/// <see cref="IUnitOfWork.ExecuteInTransactionAsync{TResult}"/> call, so one use case = one commit.
/// </summary>
public sealed class UnitOfWorkMutationCoordinator(IUnitOfWork unitOfWork) : IApplicationMutationCoordinator
{
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<TResult> RunAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return unitOfWork.ExecuteInTransactionAsync(_ => operation(), cancellationToken);
    }

    public async Task RunAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        _ = await unitOfWork.ExecuteInTransactionAsync(async _ =>
        {
            await operation();
            return true;
        }, cancellationToken);
    }
}
