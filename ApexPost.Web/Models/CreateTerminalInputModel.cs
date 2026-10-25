using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models;

public sealed class CreateTerminalInputModel
{
    [Required]
    public Guid BranchId { get; set; }

    [Required, StringLength(12, MinimumLength = 2)]
    [RegularExpression("^[A-Za-z0-9-]+$")]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;
}
