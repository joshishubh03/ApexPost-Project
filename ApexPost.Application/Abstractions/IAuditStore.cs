using ApexPost.Application.Models;

namespace ApexPost.Application.Abstractions;

/// <summary>
/// Request-level audit store used by the MVC audit filter. It writes in its own short transaction,
/// independent of the business unit of work, so an audit failure can never roll back (or be rolled
/// back by) the business change. Domain-event audits use <see cref="IAuditLogRepository"/> and share the
/// business unit of work instead. See ADR 0047.
/// </summary>
public interface IAuditStore
{
    Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default);
}
