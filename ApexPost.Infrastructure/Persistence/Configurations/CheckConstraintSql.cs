namespace ApexPost.Infrastructure.Persistence.Configurations;

/// <summary>Builds CHECK constraint SQL for status columns that are stored as enum names (varchar).</summary>
internal static class CheckConstraintSql
{
    public static string AllowedValues<TEnum>(string column, bool allowNull = false)
        where TEnum : struct, Enum => AllowedValues(column, Enum.GetNames<TEnum>(), allowNull);

    public static string AllowedValues(string column, IEnumerable<string> values, bool allowNull = false)
    {
        var list = string.Join(", ", values.Select(value => $"'{value}'"));
        return allowNull
            ? $"[{column}] IS NULL OR [{column}] IN ({list})"
            : $"[{column}] IN ({list})";
    }
}
