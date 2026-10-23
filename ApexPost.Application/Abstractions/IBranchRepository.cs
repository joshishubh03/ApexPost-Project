using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions;

public interface IBranchRepository
{
    Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAllAsync(IEnumerable<Branch> branches, CancellationToken cancellationToken = default);
}
