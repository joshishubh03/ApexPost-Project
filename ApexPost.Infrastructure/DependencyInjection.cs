using ApexPost.Application.Abstractions;
using ApexPost.Application.Events;
using ApexPost.Application.Services;
using ApexPost.Infrastructure.Events;
using ApexPost.Infrastructure.Repositories;
using ApexPost.Infrastructure.Security;
using ApexPost.Infrastructure.Seed;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddApexPostInfrastructure(
        this IServiceCollection services,
        string dataDirectory,
        string demoPassword)
    {
        services.AddSingleton<IBranchRepository>(_ => new JsonBranchRepository(dataDirectory));
        services.AddSingleton<IStaffRepository>(_ => new JsonStaffRepository(dataDirectory));
        services.AddSingleton<IProductRepository>(_ => new JsonProductRepository(dataDirectory));
        services.AddSingleton<IInventoryRepository>(_ => new JsonInventoryRepository(dataDirectory));
        services.AddSingleton<IShippingZoneRepository>(_ => new JsonShippingZoneRepository(dataDirectory));
        services.AddSingleton<IShippingRateRepository>(_ => new JsonShippingRateRepository(dataDirectory));
        services.AddSingleton<ICounterTransactionRepository>(_ => new JsonCounterTransactionRepository(dataDirectory));
        services.AddSingleton<IAuditLogRepository>(_ => new JsonAuditLogRepository(dataDirectory));
        services.AddScoped<IApplicationEventHandler<CounterTransactionCompletedEvent>, CounterTransactionAuditHandler>();
        services.AddScoped<IApplicationEventPublisher, ApplicationEventPublisher<CounterTransactionCompletedEvent>>();
        services.AddSingleton<IStaffPasswordService, AspNetStaffPasswordService>();
        services.AddSingleton(serviceProvider => new DemoDataSeeder(
            serviceProvider.GetRequiredService<IBranchRepository>(),
            serviceProvider.GetRequiredService<IStaffRepository>(),
            serviceProvider.GetRequiredService<IProductRepository>(),
            serviceProvider.GetRequiredService<IShippingZoneRepository>(),
            serviceProvider.GetRequiredService<IShippingRateRepository>(),
            serviceProvider.GetRequiredService<IStaffPasswordService>(),
            demoPassword));

        return services;
    }
}
