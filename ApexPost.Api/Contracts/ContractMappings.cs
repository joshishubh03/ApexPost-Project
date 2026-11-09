using ApexPost.Application.Courier;
using ApexPost.Domain.Courier;

namespace ApexPost.Api.Contracts;

/// <summary>Request -> command and read model -> response mapping (kept out of the controllers).</summary>
public static class ContractMappings
{
    public const string ScanIdPrefix = "scan::";

    public static CreateCourierShipmentCommand ToCommand(this CreateShipmentRequest request) => new(
        request.TrackingNumber,
        request.WeightKg,
        new Recipient(request.RecipientName, request.RecipientPhone, request.RecipientAddress, request.City));

    public static ReplaceCourierShipmentCommand ToCommand(this UpdateShipmentRequest request) => new(
        request.WeightKg,
        new Recipient(request.RecipientName, request.RecipientPhone, request.RecipientAddress, request.City));

    public static CourierShipmentQuery ToQuery(this ShipmentListQuery query) =>
        new(query.Page, query.PageSize, query.Status, query.City);

    public static IReadOnlyList<ScanUploadItem> ToItems(this ScanUploadRequest request) =>
        request.Scans.Select(scan => new ScanUploadItem(
                scan.ScanGuid,
                scan.ScanType!.Value,
                scan.CourierId,
                scan.Location,
                scan.ScannedAtUtc!.Value,
                scan.Notes,
                scan.SignatureName))
            .ToArray();

    public static ShipmentResponse ToResponse(this CourierShipmentDetails shipment) => new()
    {
        TrackingNumber = shipment.TrackingNumber,
        Status = (int)shipment.Status,
        WeightKg = shipment.WeightKilograms,
        RecipientName = shipment.Recipient.Name,
        RecipientPhone = shipment.Recipient.Phone,
        RecipientAddress = shipment.Recipient.Address,
        City = shipment.Recipient.City,
        CreatedAtUtc = shipment.CreatedAtUtc,
        UpdatedAtUtc = shipment.UpdatedAtUtc
    };

    public static ShipmentPageResponse ToResponse(this PagedResult<CourierShipmentDetails> page) => new()
    {
        Items = page.Items.Select(item => item.ToResponse()).ToArray(),
        Page = page.Page,
        PageSize = page.PageSize,
        TotalCount = page.TotalCount,
        TotalPages = page.TotalPages
    };

    public static ScanResponse ToResponse(this CourierScan scan) => new()
    {
        ScanId = ScanIdPrefix + scan.Id.ToString("D"),
        ScanType = scan.ScanType,
        CourierId = scan.CourierId,
        Location = scan.Location,
        ScannedAtUtc = scan.ScannedAtUtc,
        Notes = scan.Notes,
        SignatureName = scan.SignatureName,
        ReceivedAtUtc = scan.ReceivedAtUtc
    };

    public static ScanUploadResponse ToResponse(this ScanUploadSummary summary) => new()
    {
        Accepted = summary.Accepted,
        DuplicatesSkipped = summary.DuplicatesSkipped,
        Rejected = summary.Rejected
            .Select(rejected => new RejectedScanResponse
            {
                ScanId = ScanIdPrefix + rejected.ScanId.ToString("D"),
                Reason = rejected.Reason
            })
            .ToArray()
    };
}
