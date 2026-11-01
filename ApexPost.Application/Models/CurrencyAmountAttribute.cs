namespace ApexPost.Application.Validation;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class CurrencyAmountAttribute(bool allowZero = false, bool allowNull = false) : Attribute
{
    public string? Validate(object? value)
    {
        if (value is null && allowNull)
        {
            return null;
        }
        if (value is not decimal amount)
        {
            return "Enter a valid currency amount.";
        }
        if (amount < 0 || (!allowZero && amount == 0) || decimal.Round(amount, 2) != amount)
        {
            return allowZero
                ? "Enter a non-negative amount with no more than two decimal places."
                : "Enter a positive amount with no more than two decimal places.";
        }
        return null;
    }
}
