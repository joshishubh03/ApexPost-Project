using System.Security.Cryptography;
using System.Text;

namespace ApexPost.Web.Middleware;

public sealed class PartnerApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public const string PartnerIdItemKey = "ApexPost.PartnerId";

    private readonly IReadOnlyDictionary<string, byte[]> partnerKeyHashes = LoadPartnerKeyHashes(configuration);

    public async Task InvokeAsync(HttpContext context)
    {
        var endpointRequiresKey = context.GetEndpoint()?.Metadata
            .GetMetadata<ProtectedIntegrationEndpointAttribute>() is not null;
        if (endpointRequiresKey && !HttpMethods.IsOptions(context.Request.Method))
        {
            var providedKey = context.Request.Headers["X-API-Key"].ToString();
            if (partnerKeyHashes.Count == 0)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { error = "Partner API is not configured." });
                return;
            }

            if (string.IsNullOrWhiteSpace(providedKey) || providedKey.Length > 512)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));
            string? matchedPartnerId = null;
            foreach (var (partnerId, expectedHash) in partnerKeyHashes)
            {
                if (CryptographicOperations.FixedTimeEquals(expectedHash, providedHash))
                {
                    matchedPartnerId = partnerId;
                }
            }

            if (matchedPartnerId is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            context.Items[PartnerIdItemKey] = matchedPartnerId;
        }

        await next(context);
    }

    private static IReadOnlyDictionary<string, byte[]> LoadPartnerKeyHashes(IConfiguration configuration)
    {
        var configuredKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var partner in configuration.GetSection("PartnerApi:Partners").GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(partner.Value))
            {
                configuredKeys.Add(partner.Key, partner.Value);
            }
        }

        var legacyKey = configuration["PartnerApi:ApiKey"];
        if (!string.IsNullOrWhiteSpace(legacyKey))
        {
            configuredKeys.TryAdd("default", legacyKey);
        }

        var hashes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var uniqueKeyHashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (partnerId, key) in configuredKeys)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
            if (!uniqueKeyHashes.Add(Convert.ToHexString(hash)))
            {
                throw new InvalidOperationException("Each configured partner must have a unique API key.");
            }
            hashes.Add(partnerId, hash);
        }

        return hashes;
    }
}
