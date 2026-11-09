using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApexPost.Api.Tests;

internal static class ApiClientExtensions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static object NewShipment(string trackingNumber, decimal weightKg = 2.5m, string city = "Maplewood") => new
    {
        trackingNumber,
        weightKg,
        recipientName = "Jordan Example",
        recipientPhone = "+15550100123",
        recipientAddress = "12 Elm Row",
        city
    };

    public static object Scan(Guid id, string scanType, DateTimeOffset at, string courierId = "courier-007", string? signature = null) => new
    {
        scanId = $"scan::{id:D}",
        scanType,
        courierId,
        location = "Maplewood Depot",
        scannedAtUtc = at,
        notes = (string?)null,
        signatureName = signature
    };

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement.Clone();
    }
}
