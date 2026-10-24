using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models;

public sealed class StockAdjustmentInputModel : IValidatableObject
{
    [Required]
    public Guid? BranchId { get; set; }

    [Required]
    public Guid ProductId { get; set; }

    [Range(-1000000, 1000000)]
    public int QuantityChange { get; set; }

    [Range(0, 1000000)]
    public int ReorderLevel { get; set; }

    [Required, StringLength(200, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (QuantityChange == 0)
        {
            yield return new ValidationResult("Enter a positive or negative quantity change.", [nameof(QuantityChange)]);
        }
    }
}
