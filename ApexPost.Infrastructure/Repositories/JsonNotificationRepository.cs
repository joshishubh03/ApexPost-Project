using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;

namespace ApexPost.Infrastructure.Repositories;

public sealed class JsonNotificationRepository(string dataDirectory) : INotificationRepository
{
    private static readonly SemaphoreSlim AppendGate = new(1, 1);
    private readonly JsonCollectionStore<OperationalNotification> store =
        new(Path.Combine(dataDirectory, "notifications.json"));

    public async Task<IReadOnlyList<OperationalNotification>> GetForBranchAsync(
        Guid branchId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(limit));
        return (await store.ReadAllAsync(cancellationToken))
            .Where(notification => notification.BranchId == branchId)
            .OrderByDescending(notification => notification.OccurredAt)
            .Take(limit)
            .ToArray();
    }

    public async Task AppendAsync(OperationalNotification notification, CancellationToken cancellationToken = default)
    {
        await AppendGate.WaitAsync(cancellationToken);
        try
        {
            var notifications = (await store.ReadAllAsync(cancellationToken)).ToList();
            notifications.Add(notification);
            await store.WriteAllAsync(notifications, cancellationToken);
        }
        finally
        {
            AppendGate.Release();
        }
    }
}
