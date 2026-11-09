using System.Globalization;
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

    [Theory]
    [InlineData("ap-001", "AP-001")]
    [InlineData("  hq ", "HQ")]
    [InlineData("BRANCH-12345", "BRANCH-12345")]
    public void Branch_code_is_trimmed_and_upper_cased(string input, string expected)
    {
        Assert.Equal(expected, new BranchCode(input).Value);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("BRANCH-123456")]
    [InlineData("AP 001")]
    [InlineData("AP_001")]
    [InlineData(" ")]
    public void Branch_code_rejects_values_the_varchar_12_column_cannot_hold(string input)
    {
        Assert.ThrowsAny<ArgumentException>(() => new BranchCode(input));
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(24, true)]
    [InlineData(1, false)]
    [InlineData(25, false)]
    public void Sku_length_must_be_between_2_and_24_characters(int length, bool valid)
    {
        var value = new string('A', length);
        if (valid)
        {
            Assert.Equal(value, new Sku(value).Value);
        }
        else
        {
            Assert.Throws<ArgumentException>(() => new Sku(value));
        }
    }

    [Theory]
    [InlineData("P0005678", "****5678")]
    [InlineData("AB", "****AB")]
    [InlineData("1234567890123", "*********0123")]
    public void Identity_document_mask_keeps_only_the_last_four_characters(string input, string expected)
    {
        var masked = IdentityDocumentMask.Mask(input);

        Assert.Equal(expected, masked);
        Assert.True(IdentityDocumentMask.IsMasked(masked));
        Assert.True(masked.Length <= IdentityDocumentMask.MaxLength);
    }

    [Theory]
    [InlineData("P12345678")]
    [InlineData("***1234")]
    [InlineData("****12345")]
    [InlineData("****12*4")]
    [InlineData("*************1234")]
    [InlineData("")]
    public void Unmasked_identity_documents_are_rejected(string value)
    {
        Assert.False(IdentityDocumentMask.IsMasked(value));
        Assert.Throws<ArgumentException>(() => IdentityDocumentMask.EnsureMasked(value, nameof(value)));
    }

    [Fact]
    public void Money_sum_multiply_and_percentage_stay_in_two_decimal_places()
    {
        Assert.Equal(30.25m, Money.Sum([new Money(10.10m), new Money(20.15m)]).Amount);
        Assert.Equal(31.50m, new Money(10.50m).Multiply(3m).Amount);
        Assert.Equal(1.25m, new Money(25m).Percentage(5m).Amount);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(-0.01m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(1m).Multiply(-1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(1m).Percentage(101m));
    }

    [Theory]
    [InlineData("0.01", true)]
    [InlineData("30", true)]
    [InlineData("30.01", false)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("0.001", false)]
    public void Weight_enforces_the_30_kg_counter_limit_and_two_decimals(string kilograms, bool valid)
    {
        var value = decimal.Parse(kilograms, CultureInfo.InvariantCulture);
        if (valid)
        {
            Assert.Equal(value, new Weight(value).Kilograms);
        }
        else
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Weight(value));
        }
    }
}
