using ApexPost.Domain.ValueObjects;

namespace ApexPost.Tests;

public sealed class ValueObjectTests
{
    [Fact]
    public void Sku_normalizes_case_and_rejects_invalid_characters()
    {
        Assert.Equal("BOX-12", new Sku(" box-12 ").Value);
        Assert.Throws<ArgumentException>(() => new Sku("BOX 12"));
    }

    [Fact]
    public void Money_uses_the_currency_rounding_extension_and_rejects_extra_precision()
    {
        Assert.Equal(0.01m, 0.005m.ToCurrencyAmount());
        Assert.Equal(0.01m, new Money(10m).Percentage(0.05m).Amount);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(10.001m));
    }

    [Fact]
    public void Weight_requires_a_positive_value_with_supported_precision()
    {
        Assert.Equal(1.25m, new Weight(1.25m).Kilograms);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Weight(0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Weight(1.234m));
    }
}
