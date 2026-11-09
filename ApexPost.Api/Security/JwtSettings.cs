using Microsoft.IdentityModel.Tokens;

namespace ApexPost.Api.Security;

/// <summary>
/// JWT validation settings, bound from the "Jwt" configuration section. Issuer and audience are not secret
/// (appsettings.json); the signing key is a secret and comes from User Secrets / the deployment secret store.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";
    public const int MinimumKeyBytes = 32;

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;

    /// <summary>Base64-encoded HMAC-SHA256 key, at least 32 bytes. Never committed.</summary>
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>Lifetime of development tokens issued by /dev/token.</summary>
    public int AccessTokenMinutes { get; init; } = 60;

    public SymmetricSecurityKey CreateSigningKey() => new(Convert.FromBase64String(SigningKey)) { KeyId = "apexpost-dev-hs256" };

    /// <summary>Fails fast at startup when the configuration is incomplete.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("Configure Jwt:Issuer and Jwt:Audience.");
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(SigningKey ?? string.Empty);
        }
        catch (FormatException)
        {
            key = [];
        }

        if (key.Length < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be a Base64 key of at least 32 bytes, stored in User Secrets, e.g. " +
                "dotnet user-secrets set \"Jwt:SigningKey\" \"$([Convert]::ToBase64String((1..32 | % { Get-Random -Max 256 })))\" --project ApexPost.Api");
        }
    }
}

/// <summary>Claim and role names shared by the token issuer, the authorization rules and the courier app.</summary>
public static class ApexPostClaims
{
    public const string Role = "role";
    public const string CourierId = "courierId";
    public const string Subject = "sub";

    public const string CourierRole = "Courier";
    public const string AdminRole = "Admin";
}
