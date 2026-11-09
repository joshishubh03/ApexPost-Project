using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ApexPost.Api.Security;

/// <summary>
/// Issues HS256 development tokens with the courierId claim and Courier/Admin roles. Exposed only through the
/// Development-only /dev/token endpoint (and used by the tests). A real deployment replaces this with an identity
/// provider (see SECURITY.md).
/// </summary>
public sealed class DevelopmentTokenIssuer(JwtSettings settings, TimeProvider clock)
{
    public static readonly string[] AllowedRoles = [ApexPostClaims.CourierRole, ApexPostClaims.AdminRole];

    public IssuedToken Issue(IReadOnlyCollection<string> roles, string? courierId, TimeSpan? lifetime = null, bool expired = false)
    {
        ArgumentNullException.ThrowIfNull(roles);
        if (roles.Count == 0 || roles.Any(role => !AllowedRoles.Contains(role, StringComparer.Ordinal)))
        {
            throw new ArgumentException($"Roles must be one or more of: {string.Join(", ", AllowedRoles)}.", nameof(roles));
        }

        if (roles.Contains(ApexPostClaims.CourierRole) && string.IsNullOrWhiteSpace(courierId))
        {
            throw new ArgumentException("A Courier token needs a courierId.", nameof(courierId));
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var duration = lifetime ?? TimeSpan.FromMinutes(settings.AccessTokenMinutes);
        var (notBefore, expires) = expired
            ? (now.AddHours(-2), now.AddHours(-1))
            : (now, now.Add(duration));

        var claims = new Dictionary<string, object>
        {
            [ApexPostClaims.Subject] = courierId ?? "admin",
            [ApexPostClaims.Role] = roles.Count == 1 ? roles.First() : roles.ToArray()
        };
        if (!string.IsNullOrWhiteSpace(courierId))
        {
            claims[ApexPostClaims.CourierId] = courierId.Trim();
        }

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = notBefore,
            NotBefore = notBefore,
            Expires = expires,
            Claims = claims,
            SigningCredentials = new SigningCredentials(settings.CreateSigningKey(), SecurityAlgorithms.HmacSha256)
        });

        return new IssuedToken(token, new DateTimeOffset(expires, TimeSpan.Zero));
    }
}

/// <summary>A development access token.</summary>
public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAtUtc)
{
    public string TokenType => "Bearer";
}
