using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions;

public interface IStaffPasswordService
{
    string Hash(string password);
    bool Verify(StaffMember staff, string password);
}
