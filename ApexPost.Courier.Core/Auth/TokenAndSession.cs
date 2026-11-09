using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ApexPost.Courier.Core.Auth;

/// <summary>
/// Where the access token lives. The MAUI implementation uses SecureStorage (Keychain / Android Keystore / DPAPI);
/// never Preferences, files or logs.
/// </summary>
public interface ITokenStore
{
    Task<string?> GetAccessTokenAsync();
    Task SaveAccessTokenAsync(string accessToken);
    Task ClearAsync();
}

/// <summary>Raised when the API answers 401: the token was cleared and the UI must show the Login page.</summary>
public sealed class SessionEvents
{
    public event EventHandler<string>? SignedOut;

    public void RaiseSignedOut(string reason) => SignedOut?.Invoke(this, reason);
}

/// <summary>
/// DelegatingHandler that attaches "Authorization: Bearer {token}" from the token store to every API call. On a
/// 401 it clears the stored token and raises <see cref="SessionEvents.SignedOut"/> (the app navigates to Login).
/// The token value is never logged.
/// </summary>
public sealed class AuthHeaderHandler(ITokenStore tokens, SessionEvents session) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokens.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrWhiteSpace(token))
        {
            await tokens.ClearAsync();
            session.RaiseSignedOut("Your session has expired. Please sign in again.");
        }

        return response;
    }
}

/// <summary>Reads (without validating - the server validates) the claims the app needs from a JWT.</summary>
public static class JwtClaimsReader
{
    public sealed record TokenInfo(string? CourierId, IReadOnlyList<string> Roles, DateTimeOffset? ExpiresAtUtc)
    {
        public bool IsExpired(DateTimeOffset now) => ExpiresAtUtc is { } expires && expires <= now;
    }

    public static TokenInfo? TryRead(string? token)
    {
        var parts = token?.Split('.');
        if (parts is not { Length: 3 })
        {
            return null;
        }

        try
        {
            using var payload = JsonDocument.Parse(Base64UrlDecode(parts[1]));
            var root = payload.RootElement;
            var courierId = root.TryGetProperty("courierId", out var courier) && courier.ValueKind == JsonValueKind.String
                ? courier.GetString()
                : null;
            var roles = root.TryGetProperty("role", out var role)
                ? role.ValueKind switch
                {
                    JsonValueKind.String => [role.GetString()!],
                    JsonValueKind.Array => role.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!).ToArray(),
                    _ => Array.Empty<string>()
                }
                : Array.Empty<string>();
            DateTimeOffset? expires = root.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
            return new TokenInfo(courierId, roles, expires);
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Convert.FromBase64String(base64);
    }
}

/// <summary>In-memory token store for tests and the console (the MAUI app uses SecureStorage).</summary>
public sealed class InMemoryTokenStore : ITokenStore
{
    private string? token;

    public Task<string?> GetAccessTokenAsync() => Task.FromResult(token);

    public Task SaveAccessTokenAsync(string accessToken)
    {
        token = accessToken;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        token = null;
        return Task.CompletedTask;
    }
}
