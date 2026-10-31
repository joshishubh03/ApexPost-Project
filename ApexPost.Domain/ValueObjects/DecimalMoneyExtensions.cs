namespace ApexPost.Domain.ValueObjects;

public static class DecimalMoneyExtensions
{
    extension(decimal amount)
    {
        public decimal ToCurrencyAmount() => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }
}
