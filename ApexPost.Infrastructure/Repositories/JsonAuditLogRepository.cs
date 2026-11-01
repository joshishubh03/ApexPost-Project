using ApexPost.Application.Abstractions;
using ApexPost.Application.Models;

namespace ApexPost.Infrastructure.Repositories;

public sealed class JsonAuditLogRepository(string dataDirectory) : IAuditLogRepository
{
    private readonly string auditPath = Path.Combine(dataDirectory, "audit-records.json");
    private readonly JsonCollectionStore<AuditRecord> _store =
        new(Path.Combine(dataDirectory, "audit-records.json"));

    public Task<IReadOnlyList<AuditRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _store.ReadAllAsync(cancellationToken);

    public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default) =>
        JsonMutationCoordinator.RunStorageMutationAsync(
            Path.GetDirectoryName(auditPath) ?? ".",
            async () =>
            {
                var records = (await _store.ReadAllAsync(cancellationToken)).ToList();
                records.Add(record);
                await _store.WriteAllAsync(records, cancellationToken);
                return true;
            },
            cancellationToken);
}
