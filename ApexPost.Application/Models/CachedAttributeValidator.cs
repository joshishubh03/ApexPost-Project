using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace ApexPost.Application.Validation;

public sealed record AttributeValidationFailure(string MemberName, string ErrorMessage);

/// <summary>
/// Applies ApexPost validation attributes using cached, compiled property accessors.
/// </summary>
public sealed class CachedAttributeValidator
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<PropertyMetadata>> Metadata = new();

    public int CachedTypeCount => Metadata.Count;

    public IReadOnlyList<AttributeValidationFailure> Validate(object? model)
    {
        if (model is null)
        {
            return [];
        }

        var failures = new List<AttributeValidationFailure>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        ValidateObject(model, string.Empty, visited, failures);
        return failures;
    }

    private static void ValidateObject(
        object model,
        string prefix,
        ISet<object> visited,
        ICollection<AttributeValidationFailure> failures)
    {
        if (!model.GetType().IsValueType && !visited.Add(model))
        {
            return;
        }

        foreach (var property in Metadata.GetOrAdd(model.GetType(), BuildMetadata))
        {
            var memberName = prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}";
            var value = property.Getter(model);
            foreach (var attribute in property.CurrencyAttributes)
            {
                if (attribute.Validate(value) is { } error)
                {
                    failures.Add(new AttributeValidationFailure(memberName, error));
                }
            }

            if (value is not null && property.Recurse)
            {
                ValidateObject(value, memberName, visited, failures);
            }
        }
    }

    private static IReadOnlyList<PropertyMetadata> BuildMetadata(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetMethod is not null && property.GetIndexParameters().Length == 0)
            .Select(property => new PropertyMetadata(
                property.Name,
                CompileGetter(type, property),
                property.GetCustomAttributes<CurrencyAmountAttribute>(inherit: true).ToArray(),
                IsComplexObject(property.PropertyType)))
            .ToArray();

    private static Func<object, object?> CompileGetter(Type type, PropertyInfo property)
    {
        var instance = Expression.Parameter(typeof(object), "instance");
        var converted = Expression.Convert(instance, type);
        var member = Expression.Property(converted, property);
        var boxed = Expression.Convert(member, typeof(object));
        return Expression.Lambda<Func<object, object?>>(boxed, instance).Compile();
    }

    private static bool IsComplexObject(Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        return actualType != typeof(string) &&
            !actualType.IsPrimitive &&
            !actualType.IsEnum &&
            actualType != typeof(decimal) &&
            actualType != typeof(DateTime) &&
            actualType != typeof(DateTimeOffset) &&
            actualType != typeof(Guid) &&
            !typeof(IEnumerable).IsAssignableFrom(actualType);
    }

    private sealed record PropertyMetadata(
        string Name,
        Func<object, object?> Getter,
        IReadOnlyList<CurrencyAmountAttribute> CurrencyAttributes,
        bool Recurse);
}
