using ApexPost.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ApexPost.Infrastructure.Persistence.Conversions;

/// <summary>
/// Value converters between domain value objects and SQL Server column types. Constructing the value
/// object on read re-runs its validation, so invalid data in the database fails fast instead of leaking
/// into the domain.
/// </summary>
public static class ValueObjectConverters
{
    public static readonly ValueConverter<Sku, string> Sku =
        new(sku => sku.Value, value => new Sku(value));

    public static readonly ValueConverter<BranchCode, string> BranchCode =
        new(code => code.Value, value => new BranchCode(value));

    public static readonly ValueConverter<Money, decimal> Money =
        new(money => money.Amount, value => new Money(value));

    public static readonly ValueConverter<Weight, decimal> Weight =
        new(weight => weight.Kilograms, value => new Weight(value));
}
