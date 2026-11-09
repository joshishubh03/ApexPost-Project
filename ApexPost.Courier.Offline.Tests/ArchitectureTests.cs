using System.Reflection;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;

namespace ApexPost.Courier.Offline.Tests;

/// <summary>Domain and abstraction types must not expose or depend on Couchbase types.</summary>
public sealed class ArchitectureTests
{
    [Theory]
    [InlineData("ApexPost.Courier.Offline.Domain")]
    [InlineData("ApexPost.Courier.Offline.Abstractions")]
    public void Domain_and_abstractions_do_not_reference_couchbase_types(string ns)
    {
        var types = typeof(ShipmentScan).Assembly.GetTypes().Where(type => type.Namespace == ns).ToArray();
        Assert.NotEmpty(types);

        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                 BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var type in types)
        {
            var referenced = type.GetFields(all).Select(field => field.FieldType)
                .Concat(type.GetProperties(all).Select(property => property.PropertyType))
                .Concat(type.GetMethods(all).SelectMany(method =>
                    method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType)))
                .Concat(type.GetInterfaces())
                .Append(type.BaseType ?? typeof(object));

            Assert.DoesNotContain(referenced, candidate => IsCouchbase(candidate));
        }

        Assert.NotNull(typeof(IScanRepository));
    }

    private static bool IsCouchbase(Type type) =>
        (type.Namespace?.StartsWith("Couchbase", StringComparison.Ordinal) ?? false) ||
        (type.IsGenericType && type.GetGenericArguments().Any(IsCouchbase)) ||
        (type.HasElementType && type.GetElementType() is { } element && IsCouchbase(element));
}
