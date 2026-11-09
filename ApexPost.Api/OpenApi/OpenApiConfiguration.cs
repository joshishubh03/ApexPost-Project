using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ApexPost.Api.OpenApi;

/// <summary>
/// One OpenAPI document per API version (/openapi/v1.json, /openapi/v2.json) produced by the built-in .NET generator.
/// Documents are selected by the Asp.Versioning API Explorer group name ("v1", "v2").
/// </summary>
public static class OpenApiConfiguration
{
    public const string Title = "ApexPost Logistics API";

    public static readonly (string Name, string Label, bool Deprecated)[] Documents =
    [
        ("v2", "v2", false),
        ("v1", "v1 (deprecated)", true)
    ];

    public static IServiceCollection AddApexPostOpenApi(this IServiceCollection services)
    {
        foreach (var (name, _, deprecated) in Documents)
        {
            services.AddOpenApi(name, options => Configure(options, name, deprecated));
        }

        return services;
    }

    private static void Configure(OpenApiOptions options, string documentName, bool deprecated)
    {
        // OpenAPI 3.0 keeps the documents readable by NSwag 14 (used to generate ApexPost.Client).
        options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;

        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = Title,
                Version = documentName,
                Description = deprecated
                    ? "ApexPost courier shipments and scans - version 1 (DEPRECATED, sunset 2027-03-31). " +
                      "Flat recipient fields and numeric status codes. Migrate to v2; see VERSIONING.md."
                    : "ApexPost courier shipments and scans - version 2. Recipient grouped in a nested object, " +
                      "latestScan summary, status as a string enum. Errors are RFC 9457 Problem Details.",
                Contact = new OpenApiContact
                {
                    Name = "ApexPost Platform API Team",
                    Email = "api-team@apexpost.example",
                    Url = new Uri("https://developer.apexpost.example/api")
                }
            };
            return Task.CompletedTask;
        });

        // Every operation in the v1 document is marked deprecated (shows struck through in Swagger UI).
        options.AddOperationTransformer((operation, context, _) =>
        {
            if (deprecated || string.Equals(context.DocumentName, "v1", StringComparison.Ordinal))
            {
                operation.Deprecated = true;
            }

            return Task.CompletedTask;
        });

        // decimal -> "format": "decimal" so generated clients use decimal (money/weight) instead of double.
        options.AddSchemaTransformer((schema, context, _) =>
        {
            var type = context.JsonTypeInfo.Type;
            if (type == typeof(decimal) || type == typeof(decimal?))
            {
                schema.Format = "decimal";
            }

            return Task.CompletedTask;
        });
    }
}
