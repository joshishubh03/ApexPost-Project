using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Enums;

namespace ApexPost.Web.Models;

public sealed class CreateMoneyOrderInputModel
{
    [Required]
    public Guid? BranchId { get; set; }

    [Required, StringLength(100, MinimumLength = 2)]
    public string SenderName { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string RecipientName { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "10000000")]
    public decimal Amount { get; set; }

    [EnumDataType(typeof(IdentityDocumentType))]
    public IdentityDocumentType? DocumentType { get; set; }

    [StringLength(40, MinimumLength = 5)]
    [RegularExpression("^[A-Za-z0-9-]*$", ErrorMessage = "Use letters, numbers, and hyphens only.")]
    public string? DocumentIdentifier { get; set; }
}
