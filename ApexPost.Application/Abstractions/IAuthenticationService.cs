using ApexPost.Application.Models;

namespace ApexPost.Application.Abstractions;

public interface IAuthenticationService
{
    Task<StaffSession?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);
}
