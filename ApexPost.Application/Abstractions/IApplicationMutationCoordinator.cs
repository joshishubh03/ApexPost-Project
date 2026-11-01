namespace ApexPost.Application.Abstractions;

/// <summary>
/// Serializes a JSON-backed read/modify/write workflow across application processes.
/// </summary>
public interface IApplicationMutationCoordinator
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<TResult> RunAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken = default);

    Task RunAsync(
        Func<Task> operation,
        CancellationToken cancellationToken = default);
}
