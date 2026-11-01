namespace ApexPost.Domain.ValueObjects;

public readonly record struct Money
{
    public decimal Amount { get; }

    public Money(decimal amount)
    {
        if (amount < 0 || amount != amount.ToCurrencyAmount())
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Money must be non-negative and have no more than two decimal places.");
        }

        Amount = amount.ToCurrencyAmount();
    }

    public Money Multiply(decimal multiplier)
    {
        if (multiplier < 0)
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        return new Money((Amount * multiplier).ToCurrencyAmount());
    }

    public Money Percentage(decimal percent)
    {
        if (percent < 0 || percent > 100)
            throw new ArgumentOutOfRangeException(nameof(percent));
        return new Money((Amount * percent / 100m).ToCurrencyAmount());
    }

    public static Money Sum(IEnumerable<Money> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new Money(values.Sum(value => value.Amount).ToCurrencyAmount());
    }
}
