using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Enums;

namespace ApexPost.Web.Models;

public sealed class CreateCounterTransactionInputModel : IValidatableObject
{
    [Required]
    public Guid? BranchId { get; set; }

    public Dictionary<Guid, int> Quantities { get; set; } = [];

    [EnumDataType(typeof(PaymentMethod))]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    public Guid? ShippingRateId { get; set; }

    [Range(typeof(decimal), "0.01", "30")]
    public decimal? ShippingWeightKilograms { get; set; }

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
    }
}
