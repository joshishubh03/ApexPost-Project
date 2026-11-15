using System.Text.Json;
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
    public const string BearerScheme = "Bearer";
    public const string ApiKeyScheme = "PartnerApiKey";

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

        // Part D: security schemes so Swagger UI shows the Authorize button. JWT bearer for courier/admin endpoints,
        // X-Api-Key for the read-only partner route.
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT access token with the courierId claim and the Courier or Admin role. " +
                              "Development: GET /dev/token?roles=Courier&courierId=courier-007"
            };
            document.Components.SecuritySchemes[ApiKeyScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "X-Api-Key",
                Description = "Partner API key (read-only partner routes only)."
            };

            if (document.Paths is null)
            {
                return Task.CompletedTask;
            }

            foreach (var (path, item) in document.Paths)
            {
                if (item.Operations is null)
                {
                    continue;
                }

                var scheme = path.Contains("/partner/", StringComparison.OrdinalIgnoreCase) ? ApiKeyScheme : BearerScheme;
                foreach (var operation in item.Operations.Values)
                {
                    operation.Security =
                    [
                        new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(scheme, document)] = [] }
                    ];
                }
            }

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

        // A nullable enum property (CourierScanType?, CourierShipmentStatus? on the request DTOs, nullable so that
        // [Required] can report a missing value) is emitted as oneOf [ {enum: [null]}, {$ref: enum} ]. NSwag turns that
        // into an empty wrapper class instead of the enum, so the oneOf is collapsed to the enum reference. Run both per
        // schema and once over the finished components, so it applies whichever stage holds the references.
        options.AddSchemaTransformer((schema, _, _) =>
        {
            CollapseNullableOneOf(schema.Properties);
            return Task.CompletedTask;
        });
        options.AddDocumentTransformer((document, _, _) =>
        {
            foreach (var component in document.Components?.Schemas?.Values ?? [])
            {
                CollapseNullableOneOf((component as OpenApiSchema)?.Properties);
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

    /// <summary>Replaces <c>oneOf [ {enum: [null]}, X ]</c> property schemas with X.</summary>
    private static void CollapseNullableOneOf(IDictionary<string, IOpenApiSchema>? properties)
    {
        if (properties is null)
        {
            return;
        }

        foreach (var name in properties.Keys.ToList())
        {
            if (properties[name] is not OpenApiSchema { OneOf: { Count: 2 } options })
            {
                continue;
            }

            var real = options.Where(option => !IsNullOnly(option)).ToList();
            if (real.Count == 1)
            {
                properties[name] = real[0];
            }
        }
    }

    // The "null" branch is either { enum: [null] } or a schema whose only type is null, depending on the stage.
    private static bool IsNullOnly(IOpenApiSchema option) =>
        option is OpenApiSchema { Type: JsonSchemaType.Null } ||
        (option is OpenApiSchema { Enum: { Count: > 0 } values } &&
         values.All(value => value is null || value.GetValueKind() == JsonValueKind.Null));
}
