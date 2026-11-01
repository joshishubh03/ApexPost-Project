using ApexPost.Application.Abstractions;

namespace ApexPost.Tests;

internal sealed class ImmediateMutationCoordinator : IApplicationMutationCoordinator
{
    public static ImmediateMutationCoordinator Instance { get; } = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly AsyncLocal<int> transactionDepth = new();

    private ImmediateMutationCoordinator() { }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public async Task<TResult> RunAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        if (transactionDepth.Value > 0)
        {
            return await operation();
        }

        await gate.WaitAsync(cancellationToken);
        transactionDepth.Value++;
        try
        {
            return await operation();
        }
        finally
        {
            transactionDepth.Value--;
            gate.Release();
        }
    }

    public async Task RunAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await RunAsync(async () =>
        {
            await operation();
            return true;
        }, cancellationToken);
    }
}
