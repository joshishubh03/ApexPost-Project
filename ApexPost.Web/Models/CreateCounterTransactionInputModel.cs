using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Enums;

namespace ApexPost.Web.Models;

public sealed class CreateCounterTransactionInputModel : IValidatableObject
{
    [Required]
    public Guid? BranchId { get; set; }

    [Required]
    public Guid? TerminalId { get; set; }

    public Dictionary<Guid, int> Quantities { get; set; } = [];

    [EnumDataType(typeof(PaymentMethod))]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    public Guid? ShippingRateId { get; set; }

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

        if (ShippingRateId is not null && ShippingWeightKilograms is null)
        {
            yield return new ValidationResult("Enter a weight for the selected shipping service.", [nameof(ShippingWeightKilograms)]);
        }
        else if (ShippingWeightKilograms is { } weight && (weight <= 0 || decimal.Round(weight, 2) != weight))
        {
            yield return new ValidationResult("Parcel weight must be positive and use no more than two decimal places.", [nameof(ShippingWeightKilograms)]);
        }

        var hasAddressDetails = !string.IsNullOrWhiteSpace(ShippingRecipientName) ||
            !string.IsNullOrWhiteSpace(ShippingAddress) || ParcelLengthCentimeters is not null ||
            ParcelWidthCentimeters is not null || ParcelHeightCentimeters is not null;
        if (ShippingRateId is not null &&
            (string.IsNullOrWhiteSpace(ShippingRecipientName) || string.IsNullOrWhiteSpace(ShippingAddress) ||
             ParcelLengthCentimeters is null || ParcelWidthCentimeters is null || ParcelHeightCentimeters is null))
        {
            yield return new ValidationResult("Enter the recipient, destination address, and all parcel dimensions for the selected shipping service.",
                [nameof(ShippingRecipientName), nameof(ShippingAddress), nameof(ParcelLengthCentimeters), nameof(ParcelWidthCentimeters), nameof(ParcelHeightCentimeters)]);
        }

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
