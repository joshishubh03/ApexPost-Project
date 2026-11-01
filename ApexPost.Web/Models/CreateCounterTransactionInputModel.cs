using System.ComponentModel.DataAnnotations;
using ApexPost.Application.Validation;
using ApexPost.Domain.Enums;

namespace ApexPost.Web.Models;

public sealed class CreateCounterTransactionInputModel : IValidatableObject
{
    [Required]
    public Guid? BranchId { get; set; }

    [Required]
    public Guid? TerminalId { get; set; }

    [CurrencyAmount(allowNull: true)]
    [Range(typeof(decimal), "0.01", "10000000")]
    public decimal? MoneyOrderAmount { get; set; }

    [StringLength(100)]
    public string? MoneyOrderSenderName { get; set; }

    [StringLength(100)]
    public string? MoneyOrderRecipientName { get; set; }

    [EnumDataType(typeof(IdentityDocumentType))]
    public IdentityDocumentType? MoneyOrderDocumentType { get; set; }

    [StringLength(40)]
    public string? MoneyOrderDocumentIdentifier { get; set; }

    public Dictionary<Guid, int> Quantities { get; set; } = [];

    [EnumDataType(typeof(PaymentMethod))]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    public Guid? ShippingRateId { get; set; }

    [EnumDataType(typeof(ShipmentType))]
    public ShipmentType? ShipmentType { get; set; }

    public decimal? ShippingWeightKilograms { get; set; }

    [StringLength(100, MinimumLength = 2)]
    public string? ShippingRecipientName { get; set; }

    [StringLength(300, MinimumLength = 5)]
    public string? ShippingAddress { get; set; }

    public decimal? ParcelLengthCentimeters { get; set; }

    public decimal? ParcelWidthCentimeters { get; set; }

    public decimal? ParcelHeightCentimeters { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Quantities.Any(pair => pair.Value < 0))
        {
            yield return new ValidationResult("Product quantities cannot be negative.", [nameof(Quantities)]);
        }

        if (MoneyOrderAmount is { } moneyOrderAmount && decimal.Round(moneyOrderAmount, 2) != moneyOrderAmount)
            yield return new ValidationResult("Money order amounts must use no more than two decimal places.", [nameof(MoneyOrderAmount)]);

        if (MoneyOrderAmount is not null &&
            (string.IsNullOrWhiteSpace(MoneyOrderSenderName) || string.IsNullOrWhiteSpace(MoneyOrderRecipientName)))
            yield return new ValidationResult("Enter both the money order sender and recipient.",
                [nameof(MoneyOrderSenderName), nameof(MoneyOrderRecipientName)]);

        if (MoneyOrderAmount is null &&
            (!string.IsNullOrWhiteSpace(MoneyOrderSenderName) || !string.IsNullOrWhiteSpace(MoneyOrderRecipientName) ||
             MoneyOrderDocumentType is not null || !string.IsNullOrWhiteSpace(MoneyOrderDocumentIdentifier)))
            yield return new ValidationResult("Enter a money order amount or clear the money order details.", [nameof(MoneyOrderAmount)]);

        if (MoneyOrderDocumentType is null && !string.IsNullOrWhiteSpace(MoneyOrderDocumentIdentifier))
            yield return new ValidationResult("Select an identity document type for its identifier.",
                [nameof(MoneyOrderDocumentType), nameof(MoneyOrderDocumentIdentifier)]);

        if (ShippingRateId is not null && ShipmentType is null)
            yield return new ValidationResult("Select a shipment type.", [nameof(ShipmentType)]);

        if (ShippingRateId is not null && ShippingWeightKilograms is null)
        {
            yield return new ValidationResult("Enter a weight for the selected shipping service.", [nameof(ShippingWeightKilograms)]);
        }
        else if (ShippingWeightKilograms is { } weight && (weight <= 0 || decimal.Round(weight, 2) != weight))
        {
            yield return new ValidationResult("Shipment weight must be positive and use no more than two decimal places.", [nameof(ShippingWeightKilograms)]);
        }

        var hasAddressDetails = !string.IsNullOrWhiteSpace(ShippingRecipientName) ||
            !string.IsNullOrWhiteSpace(ShippingAddress) || ParcelLengthCentimeters is not null ||
            ParcelWidthCentimeters is not null || ParcelHeightCentimeters is not null;
        if (ShippingRateId is not null &&
            (string.IsNullOrWhiteSpace(ShippingRecipientName) || string.IsNullOrWhiteSpace(ShippingAddress)))
        {
            yield return new ValidationResult("Enter the recipient and destination address for the selected shipping service.",
                [nameof(ShippingRecipientName), nameof(ShippingAddress)]);
        }

        if (ShippingRateId is not null && ShipmentType == ApexPost.Domain.Enums.ShipmentType.Parcel &&
            (ParcelLengthCentimeters is null || ParcelWidthCentimeters is null || ParcelHeightCentimeters is null))
            yield return new ValidationResult("Enter all parcel dimensions for a parcel shipment.",
                [nameof(ParcelLengthCentimeters), nameof(ParcelWidthCentimeters), nameof(ParcelHeightCentimeters)]);

        if (ShipmentType == ApexPost.Domain.Enums.ShipmentType.Letter &&
            (ParcelLengthCentimeters is not null || ParcelWidthCentimeters is not null || ParcelHeightCentimeters is not null))
            yield return new ValidationResult("Letters do not have parcel dimensions.",
                [nameof(ParcelLengthCentimeters), nameof(ParcelWidthCentimeters), nameof(ParcelHeightCentimeters)]);

        if (ShippingRateId is null && ShipmentType is not null)
            yield return new ValidationResult("Select a shipping service or clear the shipment type.", [nameof(ShipmentType)]);

        if (ShippingRateId is null && hasAddressDetails)
        {
            yield return new ValidationResult("Select a shipping service or clear the shipment details.",
                [nameof(ShippingRateId), nameof(ShippingRecipientName), nameof(ShippingAddress)]);
        }

        foreach (var (value, fieldName) in new[]
        {
            (ParcelLengthCentimeters, nameof(ParcelLengthCentimeters)),
            (ParcelWidthCentimeters, nameof(ParcelWidthCentimeters)),
            (ParcelHeightCentimeters, nameof(ParcelHeightCentimeters))
        })
        {
            if (value is { } dimension && (dimension <= 0 || decimal.Round(dimension, 2) != dimension))
            {
                yield return new ValidationResult("Parcel dimensions must be positive and use no more than two decimal places.", [fieldName]);
            }
        }
    }
}
