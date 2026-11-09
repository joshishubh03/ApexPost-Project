namespace ApexPost.Client;

/// <summary>
/// Hand-written additions to the NSwag-generated <see cref="ApexPostApiClient"/>. The generated file is never edited;
/// this partial class adds convenience members on top of it.
/// </summary>
public partial class ApexPostApiClient
{
    public const int NotFoundStatusCode = 404;

    /// <summary>Gets a shipment, or null when the API answers 404 Not Found.</summary>
    public async Task<ShipmentResponseV2?> TryGetShipmentAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetShipmentAsync(trackingNumber, cancellationToken).ConfigureAwait(false);
        }
        catch (ApexPostApiException exception) when (exception.StatusCode == NotFoundStatusCode)
        {
            return null;
        }
    }
}
