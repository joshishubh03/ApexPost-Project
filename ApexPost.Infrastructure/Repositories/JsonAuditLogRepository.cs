using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;

namespace ApexPost.Infrastructure.Repositories;

public sealed class JsonAuditLogRepository(string dataDirectory) : IAuditLogRepository
{
    private static readonly SemaphoreSlim AppendGate = new(1, 1);
    private readonly JsonCollectionStore<AuditRecord> _store =
        new(Path.Combine(dataDirectory, "audit-records.json"));

    public Task<IReadOnlyList<AuditRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _store.ReadAllAsync(cancellationToken);

    public async Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        await AppendGate.WaitAsync(cancellationToken);
        try
        {
            var records = (await _store.ReadAllAsync(cancellationToken)).ToList();
            records.Add(record);
            await _store.WriteAllAsync(records, cancellationToken);
        }
        finally
        {
            AppendGate.Release();
        }
    }
}
