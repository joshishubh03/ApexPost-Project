using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;

namespace ApexPost.Infrastructure.Repositories;

public sealed class JsonIdentityVerificationRepository(string dataDirectory) : IIdentityVerificationRepository
{
    private readonly JsonCollectionStore<IdentityVerificationRecord> _store =
        new(Path.Combine(dataDirectory, "identity-verifications.json"));

    public Task<IReadOnlyList<IdentityVerificationRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _store.ReadAllAsync(cancellationToken);

    public Task SaveAllAsync(IEnumerable<IdentityVerificationRecord> records, CancellationToken cancellationToken = default) =>
        _store.WriteAllAsync(records, cancellationToken);
}
