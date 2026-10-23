using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;

namespace ApexPost.Infrastructure.Repositories;

public sealed class JsonStaffRepository(string dataDirectory) : IStaffRepository
{
    private readonly JsonCollectionStore<StaffMember> _store =
        new(Path.Combine(dataDirectory, "staff.json"));

    public Task<IReadOnlyList<StaffMember>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _store.ReadAllAsync(cancellationToken);

    public async Task<StaffMember?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var staff = await _store.ReadAllAsync(cancellationToken);
        return staff.FirstOrDefault(member =>
            string.Equals(member.Email, email.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public Task SaveAllAsync(IEnumerable<StaffMember> staff, CancellationToken cancellationToken = default) =>
        _store.WriteAllAsync(staff, cancellationToken);
}
