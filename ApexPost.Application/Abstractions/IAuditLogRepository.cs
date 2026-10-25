using ApexPost.Application.Models;

namespace ApexPost.Application.Abstractions;

public interface IAuditLogRepository
{
    Task<IReadOnlyList<AuditRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default);
}
