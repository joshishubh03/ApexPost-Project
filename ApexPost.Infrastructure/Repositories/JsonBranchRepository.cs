using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;

namespace ApexPost.Infrastructure.Repositories;

public sealed class JsonBranchRepository(string dataDirectory) : IBranchRepository
{
    private readonly JsonCollectionStore<Branch> _store =
        new(Path.Combine(dataDirectory, "branches.json"));

    public Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _store.ReadAllAsync(cancellationToken);

    public async Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var branches = await _store.ReadAllAsync(cancellationToken);
        return branches.FirstOrDefault(branch => branch.Id == id);
    }

    public Task SaveAllAsync(IEnumerable<Branch> branches, CancellationToken cancellationToken = default) =>
        _store.WriteAllAsync(branches, cancellationToken);
}
