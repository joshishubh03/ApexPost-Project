using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Enums;

namespace ApexPost.Web.Models;

public sealed class CreateShippingRateInputModel
{
    [Required]
    public Guid? ZoneId { get; set; }

    [EnumDataType(typeof(ShippingServiceLevel))]
    public ShippingServiceLevel ServiceLevel { get; set; } = ShippingServiceLevel.Standard;

    [Range(typeof(decimal), "0", "999999.99")]
    public decimal BasePrice { get; set; }

    [Range(typeof(decimal), "0", "999999.99")]
    public decimal PricePerKilogram { get; set; }

    [Range(typeof(decimal), "0.01", "30")]
    public decimal MaximumWeightKilograms { get; set; } = 30;
}
