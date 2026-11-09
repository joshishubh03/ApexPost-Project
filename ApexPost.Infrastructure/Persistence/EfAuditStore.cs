using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;
using ApexPost.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence;

/// <summary>
/// Request-level audit store used by the MVC audit filter. It deliberately uses its OWN short-lived
/// DbContext (own connection and transaction), not the request's unit of work:
///  * the audit row is written after the business use case has already committed or rolled back, so it can
///    never undo a sale, and a failed sale is still audited (with its 4xx/5xx status code);
///  * an audit failure is logged by the filter and never breaks the business transaction.
/// See ADR 0047.
/// </summary>
public sealed class EfAuditStore(DbContextOptions<ApexPostDbContext> options) : IAuditStore
{
    public async Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await using var auditContext = new ApexPostDbContext(options);
        auditContext.AuditEntries.Add(AuditEntryMapper.ToRow(record));
        await auditContext.SaveChangesAsync(cancellationToken);
    }
}
