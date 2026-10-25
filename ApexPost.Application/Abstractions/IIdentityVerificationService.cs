using ApexPost.Domain.Enums;

namespace ApexPost.Application.Abstractions;

/// <summary>
/// Verifies a submitted document through an integration adapter. Implementations must not persist the raw identifier.
/// </summary>
public interface IIdentityVerificationService
{
    Task<bool> VerifyAsync(
        IdentityDocumentType documentType,
        string documentIdentifier,
        CancellationToken cancellationToken = default);
}
