namespace ApexPost.Application.Models;

public sealed class TaxPolicy(IReadOnlyDictionary<string, decimal> ratesByCategory)
{
    public const string ShippingCategory = "Shipping";

    public decimal GetRatePercent(string taxCategory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taxCategory);
        var match = ratesByCategory.FirstOrDefault(entry =>
            string.Equals(entry.Key, taxCategory.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match.Key is null)
        {
            throw new InvalidOperationException($"No tax rate is configured for tax category '{taxCategory}'.");
        }

        if (match.Value < 0 || match.Value > 100 || decimal.Round(match.Value, 2) != match.Value)
        {
            throw new InvalidOperationException($"The configured tax rate for '{match.Key}' must be between 0 and 100 percent with at most two decimal places.");
        }

        return match.Value;
    }
}
