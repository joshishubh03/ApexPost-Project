using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Enums;

namespace ApexPost.Web.Models;

public sealed class UpdateStaffInputModel : IValidatableObject
{
    [Required]
    public Guid Id { get; set; }

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [EnumDataType(typeof(StaffRole))]
    public StaffRole Role { get; set; } = StaffRole.CounterClerk;

    public Guid? BranchId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Id == Guid.Empty)
        {
            yield return new ValidationResult("Select a staff account to update.", [nameof(Id)]);
        }
        if (Role != StaffRole.Administrator && BranchId is null)
        {
            yield return new ValidationResult("A branch is required for this staff role.", [nameof(BranchId)]);
        }
    }
}
