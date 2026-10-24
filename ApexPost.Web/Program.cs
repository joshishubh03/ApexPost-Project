using ApexPost.Application.Abstractions;
using ApexPost.Application.Services;
using ApexPost.Infrastructure;
using ApexPost.Infrastructure.Seed;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

var builder = WebApplication.CreateBuilder(args);
var dataDirectory = Path.GetFullPath(
    builder.Configuration["ApexPost:DataDirectory"] ??
    Path.Combine(builder.Environment.ContentRootPath, "..", "data"));
var demoPassword = builder.Configuration["DemoData:Password"] ?? "CounterDemo!2026";

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
}

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddScoped<ApexPost.Application.Abstractions.IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<StaffManagementService>();
builder.Services.AddScoped<ProductCatalogueService>();
builder.Services.AddScoped<InventoryManagementService>();
builder.Services.AddApexPostInfrastructure(dataDirectory, demoPassword);
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

await app.RunAsync();

public partial class Program { }
