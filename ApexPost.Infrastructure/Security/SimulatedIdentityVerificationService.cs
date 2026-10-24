using ApexPost.Application.Abstractions;
using ApexPost.Domain.Enums;

namespace ApexPost.Infrastructure.Security;

/// <summary>
/// Local training adapter. It accepts only synthetic demo identifiers with the configured success prefix.
/// Replace this adapter with the approved identity provider before production use.
/// </summary>
public sealed class SimulatedIdentityVerificationService(string successPrefix) : IIdentityVerificationService
{
    public Task<bool> VerifyAsync(
        IdentityDocumentType documentType,
        string documentIdentifier,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(documentIdentifier);
        if (!Enum.IsDefined(documentType))
        {
            throw new ArgumentOutOfRangeException(nameof(documentType));
        }

        return Task.FromResult(documentIdentifier.StartsWith(successPrefix, StringComparison.OrdinalIgnoreCase));
    }
}
