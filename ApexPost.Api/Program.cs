using System.Diagnostics;
using System.Text.Json.Serialization;
using ApexPost.Api.ErrorHandling;
using ApexPost.Api.OpenApi;
using ApexPost.Api.Routing;
using ApexPost.Api.Security;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Rewrite;
using ApexPost.Infrastructure;
using ApexPost.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

// Week 4 ApexPost.Api - minimal hosting (no Startup.cs). Reuses the Week 3 SQL Server / EF Core composition.
var builder = WebApplication.CreateBuilder(args);

// The connection string comes from User Secrets (Development) or the environment - never from appsettings.json.
var connectionString = builder.Configuration.GetConnectionString("ApexPostDb")
    ?? Environment.GetEnvironmentVariable("APEXPOST_DB_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No ApexPostDb connection string is configured. Run: dotnet user-secrets set \"ConnectionStrings:ApexPostDb\" \"<connection string>\" --project ApexPost.Api");
}

// Existing DbContext, unit of work and repositories (Week 3) + the Week 4 courier shipment service.
builder.Services.AddApexPostSqlServer(connectionString, builder.Configuration.GetValue("Persistence:CommandTimeoutSeconds", 30));
builder.Services.AddApexPostCourierShipments();

builder.Services.Configure<RouteOptions>(options =>
    options.ConstraintMap[TrackingNumberRouteConstraint.Name] = typeof(TrackingNumberRouteConstraint));

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Part D: JWT bearer authentication. Issuer/audience/lifetime/signing key are all validated; values come from
// configuration ("Jwt" section) and the signing key from User Secrets.
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
jwtSettings.Validate();
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<DevelopmentTokenIssuer>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtSettings.CreateSigningKey(),
            RoleClaimType = ApexPostClaims.Role,
            NameClaimType = ApexPostClaims.Subject
        };
        options.Events = new JwtBearerEvents
        {
            // 401/403 bodies are written as Problem Details by UseStatusCodePages; here we only explain *why*.
            OnChallenge = context =>
            {
                if (context.AuthenticateFailure is SecurityTokenExpiredException)
                {
                    context.HttpContext.Items[AuthProblemKey] = "The access token has expired. Sign in again.";
                }
                else if (context.AuthenticateFailure is not null)
                {
                    context.HttpContext.Items[AuthProblemKey] = "The access token is invalid.";
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// Partner API keys (User Secrets "PartnerApi:Keys:{partner}"), hashed in memory.
builder.Services.AddSingleton<PartnerApiKeyStore>();

// HSTS outside Development: browsers must use HTTPS for a year, including subdomains.
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

// Problem Details for every error: validation (ValidationProblemDetails via [ApiController]), status-code pages
// (404/405/415 with no body) and exceptions (ApiExceptionHandler).
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
    context.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
    switch (context.ProblemDetails.Status)
    {
        case StatusCodes.Status401Unauthorized:
            context.ProblemDetails.Detail ??= context.HttpContext.Items[AuthProblemKey] as string
                ?? "A valid bearer token is required (Authorization: Bearer <token>).";
            break;
        case StatusCodes.Status403Forbidden:
            context.ProblemDetails.Detail ??= "Your role does not allow this operation.";
            break;
    }
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// Part C: URL-segment versioning (/api/v{version}/shipments). v1 = the Part B contract (deprecated), v2 = new shape.
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(2, 0);
        options.AssumeDefaultVersionWhenUnspecified = false;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
        // Adds api-supported-versions / api-deprecated-versions response headers.
        options.ReportApiVersions = true;
        // Adds a Sunset header (RFC 8594) with a link to the migration guide on every v1 response.
        options.Policies.Sunset(1.0)
            .Effective(new DateTimeOffset(2027, 3, 31, 0, 0, 0, TimeSpan.Zero))
            .Link("https://developer.apexpost.example/api/versioning")
            .Title("ApexPost API v1 sunset and migration guide")
            .Type("text/html");
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        // Group names "v1" / "v2" select the version-specific OpenAPI documents.
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddApexPostOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Transport security: always redirect HTTP to HTTPS; HSTS outside Development (a dev certificate must not be pinned
// into the browser's HSTS list). Bearer tokens and API keys must never travel over plain HTTP - see SECURITY.md.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// Part B clients that still call the unversioned /api/shipments routes are served by v1 (deprecated) so they keep
// working until the v1 sunset date. The rewrite must run before routing.
app.UseRewriter(new RewriteOptions().AddRewrite(@"^api/shipments(/.*)?$", "api/v1/shipments$1", skipRemainingRules: true));
app.UseRouting();

app.UseWhen(context => PartnerApiKeyMiddleware.IsPartnerPath(context.Request.Path),
    partner => partner.UseMiddleware<PartnerApiKeyMiddleware>());
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    // OpenAPI documents and Swagger UI are Development-only. Hiding documentation is NOT a security control:
    // the endpoints are protected by authentication/authorization, not by being undocumented.
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.DocumentTitle = OpenApiConfiguration.Title;
        foreach (var (name, label, _) in OpenApiConfiguration.Documents)
        {
            options.SwaggerEndpoint($"/openapi/{name}.json", label);
        }
    });
}

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    // Development-only token endpoint (D1). Example: /dev/token?roles=Courier&courierId=courier-007
    // roles: Courier, Admin or "Admin,Courier"; expired=true issues an already-expired token for the 401 demo.
    app.MapGet("/dev/token", (string roles, string? courierId, bool? expired, DevelopmentTokenIssuer issuer) =>
        {
            try
            {
                var roleList = roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return Results.Ok(issuer.Issue(roleList, courierId, expired: expired == true));
            }
            catch (ArgumentException exception)
            {
                return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest, title: "Invalid token request.");
            }
        })
        .AllowAnonymous()
        .ExcludeFromDescription();
}

if (app.Environment.IsDevelopment() &&
    app.Configuration.GetValue("Persistence:ApplyMigrationsOnStartup", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ApexPostDbContext>().Database.MigrateAsync();
}

await app.RunAsync();

public partial class Program
{
    /// <summary>HttpContext.Items key carrying why authentication failed (expired vs invalid token).</summary>
    internal const string AuthProblemKey = "ApexPost.AuthProblem";
}
