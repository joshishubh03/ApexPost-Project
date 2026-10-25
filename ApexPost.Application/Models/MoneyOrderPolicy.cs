namespace ApexPost.Application.Models;

/// <summary>
/// Configurable values for the money order workflow; defaults live in Web configuration.
/// </summary>
public sealed record MoneyOrderPolicy(
    decimal IdentityVerificationThreshold,
    decimal FeeAmount,
    string DemoVerificationSuccessPrefix);
