using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions;

/// <summary>Identity verifications are append-only and only ever hold a masked document identifier.</summary>
public interface IIdentityVerificationRepository
{
    Task<IReadOnlyList<IdentityVerificationRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAllAsync(IEnumerable<IdentityVerificationRecord> records, CancellationToken cancellationToken = default);

    async Task AddAsync(IdentityVerificationRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        var all = (await GetAllAsync(cancellationToken)).ToList();
        all.Add(record);
        await SaveAllAsync(all, cancellationToken);
    }
}
