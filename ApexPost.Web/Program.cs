using System.Threading.RateLimiting;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Services;
using ApexPost.Infrastructure;
using ApexPost.Infrastructure.Seed;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var dataDirectory = Path.GetFullPath(
    builder.Configuration["ApexPost:DataDirectory"] ??
    Path.Combine(builder.Environment.ContentRootPath, "..", "data"));
var demoPassword = builder.Configuration["DemoData:Password"] ?? "CounterDemo!2026";
var moneyOrderPolicy = new ApexPost.Application.Models.MoneyOrderPolicy(
    builder.Configuration.GetValue<decimal>("MoneyOrders:IdentityVerificationThreshold", 10_000m),
    builder.Configuration.GetValue<decimal>("MoneyOrders:FeeAmount", 50m),
    builder.Configuration["MoneyOrders:DemoVerificationSuccessPrefix"] ?? "DEMO-VALID-");
var taxRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
{
    ["Standard"] = 0m,
    [ApexPost.Application.Models.TaxPolicy.ShippingCategory] = 0m
};
var configuredTaxRates = builder.Configuration.GetSection("Taxation:Rates").Get<Dictionary<string, decimal>>();
if (configuredTaxRates is not null)
{
    foreach (var (category, rate) in configuredTaxRates)
    {
        taxRates[category] = rate;
    }
}
var taxPolicy = new ApexPost.Application.Models.TaxPolicy(taxRates);

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
}

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.AddService<RequestTimingActionFilter>();
    options.Filters.AddService<MvcAuditActionFilter>();
    options.Filters.AddService<ModelValidationActionFilter>();
});
builder.Services.AddScoped<RequestTimingActionFilter>();
builder.Services.AddScoped<MvcAuditActionFilter>();
builder.Services.AddScoped<ModelValidationActionFilter>();
builder.Services.AddExceptionHandler<SafeProblemDetailsExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<ApexPost.Application.Abstractions.IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<StaffManagementService>();
builder.Services.AddScoped<ProductCatalogueService>();
builder.Services.AddScoped<InventoryManagementService>();
builder.Services.AddScoped<ShippingRateService>();
builder.Services.AddScoped<CounterTransactionService>();
builder.Services.AddScoped<MoneyOrderService>();
builder.Services.AddScoped<ReportingService>();
builder.Services.AddSingleton(ApexPost.Application.Models.LowStockRule.AtOrBelowReorderLevel);
builder.Services.AddScoped<PartnerTrackingService>();
builder.Services.AddSingleton(moneyOrderPolicy);
builder.Services.AddSingleton(taxPolicy);
builder.Services.AddCors(options =>
{
    var origins = builder.Configuration.GetSection("PartnerApi:AllowedCorsOrigins").Get<string[]>() ?? [];
    options.AddPolicy("TrackingWidget", policy => policy
        .WithOrigins(origins)
        .WithMethods("GET")
        .WithHeaders("X-API-Key", "Content-Type"));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("partner", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Items.TryGetValue(PartnerApiKeyMiddleware.PartnerIdItemKey, out var partnerId) &&
            partnerId is string configuredPartnerId
                ? configuredPartnerId
                : context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
builder.Services.AddApexPostInfrastructure(
    dataDirectory,
    demoPassword,
    moneyOrderPolicy,
    builder.Environment.IsDevelopment());
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "ApexPost.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
else
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
}

app.UseMiddleware<RequestExceptionLoggingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<MaintenanceModeMiddleware>();

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors();
app.UseMiddleware<EndpointIpRestrictionMiddleware>();
app.UseMiddleware<PartnerApiKeyMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

await app.RunAsync();

public partial class Program { }
