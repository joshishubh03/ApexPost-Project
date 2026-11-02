using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models;

/// <summary>Request DTO for the set-based price revision (only these two fields are bound).</summary>
public sealed class RevisePricesInputModel
{
    [Required]
    [StringLength(60, MinimumLength = 2)]
    public string Category { get; set; } = string.Empty;

    [Range(typeof(decimal), "-50", "100")]
    public decimal PercentChange { get; set; }
}
