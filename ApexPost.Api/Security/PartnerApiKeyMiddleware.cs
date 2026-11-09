using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Security;

/// <summary>
/// Partner API keys: "PartnerApi:Keys:{partnerName}" = key, from User Secrets or the deployment secret store (never
/// committed). Keys are kept only as SHA-256 hashes in memory and compared in constant time.
/// </summary>
public sealed class PartnerApiKeyStore
{
    public const string HeaderName = "X-Api-Key";
    public const string PartnerNameItem = "ApexPost.PartnerName";

    private readonly (string Partner, byte[] Hash)[] keys;

    public PartnerApiKeyStore(IConfiguration configuration)
    {
        keys = configuration.GetSection("PartnerApi:Keys").GetChildren()
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
            .Select(entry => (entry.Key, Hash(entry.Value!)))
            .ToArray();
    }

    public bool HasKeys => keys.Length > 0;

    /// <summary>Returns the partner name for a valid key, otherwise null.</summary>
    public string? FindPartner(string? presentedKey)
    {
        if (string.IsNullOrWhiteSpace(presentedKey))
        {
            return null;
        }

        var presented = Hash(presentedKey);
        string? match = null;
        foreach (var (partner, hash) in keys)
        {
            // Compare every key in constant time so timing does not reveal which key nearly matched.
            if (CryptographicOperations.FixedTimeEquals(presented, hash))
            {
                match = partner;
            }
        }

        return match;
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}

/// <summary>
/// Guards the read-only partner routes (/api/v{n}/partner/...): a missing or invalid X-Api-Key gets 401 Problem
/// Details. The key itself is never logged.
/// </summary>
public sealed class PartnerApiKeyMiddleware(RequestDelegate next, PartnerApiKeyStore store, ILogger<PartnerApiKeyMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetails)
    {
        var partner = store.FindPartner(context.Request.Headers[PartnerApiKeyStore.HeaderName].ToString());
        if (partner is null)
        {
            logger.LogWarning("Rejected partner request to {Path}: missing or invalid API key", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = $"ApiKey header=\"{PartnerApiKeyStore.HeaderName}\"";
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "A valid partner API key is required.",
                Detail = $"Send your partner key in the {PartnerApiKeyStore.HeaderName} header.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                Instance = context.Request.Path
            };
            problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
            await problemDetails.WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
            return;
        }

        context.Items[PartnerApiKeyStore.PartnerNameItem] = partner;
        using (logger.BeginScope(new Dictionary<string, object> { ["Partner"] = partner }))
        {
            await next(context);
        }
    }

    public static bool IsPartnerPath(PathString path) =>
        path.Value is { } value &&
        value.Contains("/partner/", StringComparison.OrdinalIgnoreCase) &&
        value.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);
}
