using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models;

public sealed class ShippingQuoteInputModel
{
    [Required]
    public Guid? RateId { get; set; }

    [Range(typeof(decimal), "0.01", "30")]
    public decimal WeightKilograms { get; set; } = 1;
}
