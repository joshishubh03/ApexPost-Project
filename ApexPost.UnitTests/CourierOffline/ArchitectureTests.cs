using System.Reflection;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Storage;

namespace ApexPost.Courier.Offline.Tests;

/// <summary>
/// The courier domain and its ports live in ApexPost.Courier.Offline.Domain, a project with no package references,
/// so the compiler already rejects any Couchbase type there. These tests document the rule and catch a reference
/// being added to that project later.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(ShipmentScan).Assembly;

    [Fact]
    public void Domain_and_ports_live_in_their_own_assembly_separate_from_the_couchbase_adapter()
    {
        Assert.Equal("ApexPost.Courier.Offline.Domain", DomainAssembly.GetName().Name);
        Assert.Same(DomainAssembly, typeof(IScanRepository).Assembly);
        Assert.Same(DomainAssembly, typeof(IShipmentUploader).Assembly);
        Assert.NotSame(DomainAssembly, typeof(CouchbaseScanRepository).Assembly);
    }

    [Fact]
    public void Domain_assembly_references_no_couchbase_or_other_apexpost_assembly()
    {
        var referenced = DomainAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(referenced, name => name.StartsWith("Couchbase", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referenced, name => name.StartsWith("ApexPost", StringComparison.Ordinal));
    }

    [Fact]
    public void Couchbase_types_appear_only_in_the_storage_namespace_of_the_adapter()
    {
        var adapterAssembly = typeof(CouchbaseScanRepository).Assembly;
        Assert.Contains(adapterAssembly.GetReferencedAssemblies(),
            reference => reference.Name?.StartsWith("Couchbase", StringComparison.OrdinalIgnoreCase) == true);

        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                 BindingFlags.Static | BindingFlags.DeclaredOnly;
        var publicTypesOutsideStorage = adapterAssembly.GetExportedTypes()
            .Where(type => type.Namespace != typeof(CouchbaseScanRepository).Namespace);

        foreach (var type in publicTypesOutsideStorage)
        {
            var exposed = type.GetProperties(all).Where(property => property.GetMethod?.IsPublic == true)
                .Select(property => property.PropertyType)
                .Concat(type.GetMethods(all).Where(method => method.IsPublic).SelectMany(method =>
                    method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType)))
                .Concat(type.GetConstructors().SelectMany(constructor =>
                    constructor.GetParameters().Select(parameter => parameter.ParameterType)));

            Assert.DoesNotContain(exposed, IsCouchbase);
        }
    }

    private static bool IsCouchbase(Type type) =>
        (type.Namespace?.StartsWith("Couchbase", StringComparison.Ordinal) ?? false) ||
        (type.IsGenericType && type.GetGenericArguments().Any(IsCouchbase)) ||
        (type.HasElementType && type.GetElementType() is { } element && IsCouchbase(element));
}
