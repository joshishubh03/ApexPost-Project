using System.Text.Json;

namespace ApexPost.Specs.Support;

/// <summary>
/// Per-scenario state for the API scenarios, shared between step classes through Reqnroll context injection
/// (a new instance for every scenario; no static fields).
/// </summary>
public sealed class ShipmentApiContext : IDisposable
{
    public HttpClient? Client { get; set; }

    public HttpResponseMessage? LastResponse { get; private set; }

    public JsonElement LastBody { get; private set; }

    /// <summary>Tracking numbers this scenario created; the AfterScenario hook deletes them.</summary>
    public HashSet<string> CreatedTrackingNumbers { get; } = new(StringComparer.Ordinal);

    public HttpClient RequireClient() =>
        Client ?? throw new InvalidOperationException("No API client: the Background step 'I am signed in...' did not run.");

    public async Task RememberAsync(HttpResponseMessage response)
    {
        LastResponse?.Dispose();
        LastResponse = response;
        var body = await response.Content.ReadAsStringAsync();
        LastBody = string.IsNullOrWhiteSpace(body) ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    public void Dispose()
    {
        LastResponse?.Dispose();
        Client?.Dispose();
    }
}
