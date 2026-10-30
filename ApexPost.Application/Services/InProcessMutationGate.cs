namespace ApexPost.Application.Services;

internal static class InProcessMutationGate
{
    public static SemaphoreSlim CounterAndInventory { get; } = new(1, 1);
    public static SemaphoreSlim MoneyOrders { get; } = new(1, 1);

    public static async Task<TResult> RunAsync<TResult>(
        SemaphoreSlim gate,
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await operation();
        }
        finally
        {
            gate.Release();
        }
    }

    public static async Task RunAsync(
        SemaphoreSlim gate,
        Func<Task> operation,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await operation();
        }
        finally
        {
            gate.Release();
        }
    }
}
