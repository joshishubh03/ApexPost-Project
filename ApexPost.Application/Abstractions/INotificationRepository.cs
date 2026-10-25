using ApexPost.Application.Models;

namespace ApexPost.Application.Abstractions;

public interface INotificationRepository
{
    Task<IReadOnlyList<OperationalNotification>> GetForBranchAsync(
        Guid branchId,
        int limit = 20,
        CancellationToken cancellationToken = default);

    Task AppendAsync(OperationalNotification notification, CancellationToken cancellationToken = default);
}
