using ApexPost.Application.Validation;

namespace ApexPost.Tests;

public sealed class CachedAttributeValidatorTests
{
    [Fact]
    public void Reflection_validator_checks_nested_currency_attributes_and_reuses_cached_metadata()
    {
        var validator = new CachedAttributeValidator();
        var model = new CheckoutModel { Line = new CheckoutLine { Amount = 12.345m } };

        var firstFailures = validator.Validate(model);
        var cachedTypeCount = validator.CachedTypeCount;
        var secondFailures = validator.Validate(model);

        var failure = Assert.Single(firstFailures);
        Assert.Equal("Line.Amount", failure.MemberName);
        Assert.Contains("two decimal places", failure.ErrorMessage);
        Assert.Equal(firstFailures, secondFailures);
        Assert.Equal(cachedTypeCount, validator.CachedTypeCount);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("0.00", false)]
    [InlineData("12.34", true)]
    [InlineData("-1.00", false)]
    [InlineData("1.234", false)]
    public void Currency_attribute_applies_optional_positive_amount_rules(string? amountText, bool isValid)
    {
        var attribute = new CurrencyAmountAttribute(allowNull: true);
        object? amount = amountText is null ? null : decimal.Parse(amountText, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(isValid, attribute.Validate(amount) is null);
    }

    private sealed class CheckoutModel
    {
        public CheckoutLine Line { get; init; } = new();
    }

    private sealed class CheckoutLine
    {
        [CurrencyAmount(allowNull: true)]
        public decimal? Amount { get; init; }
    }
}
