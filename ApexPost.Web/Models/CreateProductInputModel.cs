using System.ComponentModel.DataAnnotations;
using ApexPost.Application.Validation;

namespace ApexPost.Web.Models;

public sealed class CreateProductInputModel
{
    [Required, StringLength(24, MinimumLength = 2)]
    [RegularExpression("^[A-Za-z0-9-]+$", ErrorMessage = "Use letters, numbers, or hyphens.")]
    public string Sku { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string Category { get; set; } = string.Empty;

    [Required, StringLength(60, MinimumLength = 1)]
    public string TaxCategory { get; set; } = "Standard";

    [CurrencyAmount(allowZero: true)]
    [Range(typeof(decimal), "0", "9999999.99")]
    public decimal Price { get; set; }
}
