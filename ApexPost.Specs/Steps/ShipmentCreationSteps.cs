using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApexPost.Specs.Support;

namespace ApexPost.Specs.Steps;

/// <summary>Steps for ShipmentCreation.feature. State lives in <see cref="ShipmentApiContext"/> (context injection).</summary>
[Binding]
public sealed class ShipmentCreationSteps(ShipmentApiContext context)
{
    private const string ShipmentsV2 = "/api/v2/shipments";

    [Given(@"^I am signed in to the shipment API as an administrator$")]
    public void GivenIAmSignedInAsAnAdministrator() =>
        context.Client = TestRunHooks.ApiHost.CreateClientWithRoles("Admin");

    [Given(@"^a shipment with tracking number ""(.*)"" for ""(.*)"" already exists$")]
    public async Task GivenAShipmentAlreadyExists(string trackingNumber, string recipient)
    {
        using var response = await PostShipmentAsync(trackingNumber, 2.5m, recipient, "Maplewood");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [When(@"^I create a shipment with tracking number ""(.*)"" weighing (.*) kg for ""(.*)"" in ""(.*)""$")]
    public async Task WhenICreateAShipment(string trackingNumber, decimal weightKg, string recipient, string city) =>
        await context.RememberAsync(await PostShipmentAsync(trackingNumber, weightKg, recipient, city));

    [Then(@"^the shipment is created$")]
    public void ThenTheShipmentIsCreated() =>
        Assert.Equal(HttpStatusCode.Created, Response().StatusCode);

    [Then(@"^the response tells me where to find shipment ""(.*)""$")]
    public void ThenTheResponseTellsMeWhereToFindIt(string trackingNumber)
    {
        var location = Response().Headers.Location;
        Assert.NotNull(location);
        Assert.EndsWith($"{ShipmentsV2}/{trackingNumber}", location.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Then(@"^tracking ""(.*)"" shows the status ""(.*)""$")]
    public async Task ThenTrackingShowsTheStatus(string trackingNumber, string status)
    {
        var shipment = await GetShipmentAsync(trackingNumber);
        Assert.Equal(status, shipment.GetProperty("status").GetString());
    }

    [Then(@"^the shipment is refused as a duplicate$")]
    public void ThenTheShipmentIsRefusedAsADuplicate()
    {
        Assert.Equal(HttpStatusCode.Conflict, Response().StatusCode);
        Assert.Equal("application/problem+json", Response().Content.Headers.ContentType?.MediaType);
    }

    [Then(@"^shipment ""(.*)"" still belongs to ""(.*)""$")]
    public async Task ThenShipmentStillBelongsTo(string trackingNumber, string recipient)
    {
        var shipment = await GetShipmentAsync(trackingNumber);
        Assert.Equal(recipient, shipment.GetProperty("recipient").GetProperty("name").GetString());
    }

    [Then(@"^the shipment is refused because ""(.*)""$")]
    public void ThenTheShipmentIsRefusedBecause(string reason)
    {
        Assert.Equal(HttpStatusCode.BadRequest, Response().StatusCode);
        var messages = context.LastBody.GetProperty("errors").EnumerateObject()
            .SelectMany(field => field.Value.EnumerateArray().Select(message => message.GetString()))
            .ToList();
        Assert.Contains(reason, messages);
    }

    [Then(@"^no shipment ""(.*)"" exists$")]
    public async Task ThenNoShipmentExists(string trackingNumber)
    {
        using var response = await context.RequireClient().GetAsync($"{ShipmentsV2}/{trackingNumber}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpResponseMessage Response() =>
        context.LastResponse ?? throw new InvalidOperationException("No request has been sent in this scenario.");

    private async Task<HttpResponseMessage> PostShipmentAsync(string trackingNumber, decimal weightKg, string recipient, string city)
    {
        var response = await context.RequireClient().PostAsJsonAsync(ShipmentsV2, new
        {
            trackingNumber,
            weightKg,
            recipientName = recipient,
            recipientPhone = "+15550100123",
            recipientAddress = "12 Elm Row",
            city
        });

        if (response.StatusCode == HttpStatusCode.Created)
        {
            context.CreatedTrackingNumbers.Add(trackingNumber);
        }

        return response;
    }

    private async Task<JsonElement> GetShipmentAsync(string trackingNumber)
    {
        using var response = await context.RequireClient().GetAsync($"{ShipmentsV2}/{trackingNumber}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }
}
