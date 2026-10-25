using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions;

public interface IIdentityVerificationRepository
{
    Task<IReadOnlyList<IdentityVerificationRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAllAsync(IEnumerable<IdentityVerificationRecord> records, CancellationToken cancellationToken = default);
}
