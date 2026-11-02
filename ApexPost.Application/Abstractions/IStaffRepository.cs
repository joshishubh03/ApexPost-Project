using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions;

public interface IStaffRepository
{
    Task<IReadOnlyList<StaffMember>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<StaffMember?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task SaveAllAsync(IEnumerable<StaffMember> staff, CancellationToken cancellationToken = default);

    async Task<StaffMember?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await GetAllAsync(cancellationToken)).FirstOrDefault(member => member.Id == id);
}
