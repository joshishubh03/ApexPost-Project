using System.Diagnostics;
using System.Text.Json.Serialization;
using ApexPost.Api.ErrorHandling;
using ApexPost.Api.Routing;
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

// Problem Details for every error: validation (ValidationProblemDetails via [ApiController]), status-code pages
// (404/405/415 with no body) and exceptions (ApiExceptionHandler).
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
    context.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapControllers();

if (app.Environment.IsDevelopment() &&
    app.Configuration.GetValue("Persistence:ApplyMigrationsOnStartup", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ApexPostDbContext>().Database.MigrateAsync();
}

await app.RunAsync();
