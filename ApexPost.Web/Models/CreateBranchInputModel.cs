using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models;

public sealed class CreateBranchInputModel
{
    [Required, StringLength(12, MinimumLength = 2)]
    [RegularExpression("^[A-Za-z0-9-]+$", ErrorMessage = "Use letters, numbers, or hyphens.")]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Region { get; set; } = string.Empty;
}
