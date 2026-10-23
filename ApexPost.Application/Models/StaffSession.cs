using ApexPost.Domain.Enums;

namespace ApexPost.Application.Models;

public sealed record StaffSession(
    Guid StaffId,
    string Email,
    string FullName,
    StaffRole Role,
    Guid? BranchId,
    string? BranchCode,
    string? BranchName);
