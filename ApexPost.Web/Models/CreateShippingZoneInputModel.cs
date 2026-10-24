using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models;

public sealed class CreateShippingZoneInputModel
{
    [Required, StringLength(12, MinimumLength = 2)]
    [RegularExpression("^[A-Za-z0-9-]+$", ErrorMessage = "Use letters, numbers, or hyphens.")]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(80, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 2)]
    public string Destination { get; set; } = string.Empty;
}
