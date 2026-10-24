using ApexPost.Application.Abstractions;
using ApexPost.Domain.Enums;

namespace ApexPost.Infrastructure.Security;

/// <summary>
/// Fails closed until a real identity provider is configured.
/// </summary>
public sealed class UnconfiguredIdentityVerificationService : IIdentityVerificationService
{
    public Task<bool> VerifyAsync(
        IdentityDocumentType documentType,
        string documentIdentifier,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }
}
