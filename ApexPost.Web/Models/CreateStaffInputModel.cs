using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Enums;

namespace ApexPost.Web.Models;

public sealed class CreateStaffInputModel : IValidatableObject
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [EnumDataType(typeof(StaffRole))]
    public StaffRole Role { get; set; } = StaffRole.CounterClerk;

    public Guid? BranchId { get; set; }

    [Required, StringLength(128, MinimumLength = 12)]
    [DataType(DataType.Password)]
    public string InitialPassword { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Role != StaffRole.Administrator && BranchId is null)
        {
            yield return new ValidationResult("A branch is required for this role.", [nameof(BranchId)]);
        }
    }
}
