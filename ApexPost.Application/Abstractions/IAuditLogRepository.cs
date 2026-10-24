using ApexPost.Application.Models;

namespace ApexPost.Application.Abstractions;

public interface IAuditLogRepository
{
    Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default);
}
