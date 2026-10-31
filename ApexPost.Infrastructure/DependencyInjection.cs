using ApexPost.Application.Abstractions;
using ApexPost.Application.Events;
using ApexPost.Application.Models;
using ApexPost.Application.Services;
using ApexPost.Infrastructure.Events;
using ApexPost.Infrastructure.Repositories;
using ApexPost.Infrastructure.Security;
using ApexPost.Infrastructure.Seed;
using ApexPost.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddApexPostSqlServer(
        this IServiceCollection services,
        string connectionString,
        int commandTimeoutSeconds = 30)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (commandTimeoutSeconds is < 1 or > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(commandTimeoutSeconds));
        }

        services.AddDbContext<ApexPostDbContext>(options => options.UseSqlServer(
            connectionString,
            sql => sql
                .CommandTimeout(commandTimeoutSeconds)
                .EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)
                .MigrationsAssembly(typeof(ApexPostDbContext).Assembly.GetName().Name)));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        return services;
    }

    public static IServiceCollection AddApexPostInfrastructure(
        this IServiceCollection services,
        string dataDirectory,
        string demoPassword,
        MoneyOrderPolicy moneyOrderPolicy,
        bool enableSimulatedIdentityVerification)
    {
        services.AddSingleton<IApplicationMutationCoordinator>(_ => new JsonMutationCoordinator(dataDirectory));
        services.AddSingleton<IBranchRepository>(_ => new JsonBranchRepository(dataDirectory));
        services.AddSingleton<IStaffRepository>(_ => new JsonStaffRepository(dataDirectory));
        services.AddSingleton<IProductRepository>(_ => new JsonProductRepository(dataDirectory));
        services.AddSingleton<IInventoryRepository>(_ => new JsonInventoryRepository(dataDirectory));
        services.AddSingleton<IShippingZoneRepository>(_ => new JsonShippingZoneRepository(dataDirectory));
        services.AddSingleton<IShippingRateRepository>(_ => new JsonShippingRateRepository(dataDirectory));
        services.AddSingleton<ICounterTransactionRepository>(_ => new JsonCounterTransactionRepository(dataDirectory));
        services.AddSingleton<ICounterTerminalRepository>(_ => new JsonCounterTerminalRepository(dataDirectory));
        services.AddSingleton<IMoneyOrderRepository>(_ => new JsonMoneyOrderRepository(dataDirectory));
        services.AddSingleton<IIdentityVerificationRepository>(_ => new JsonIdentityVerificationRepository(dataDirectory));
        services.AddSingleton<IAuditLogRepository>(_ => new JsonAuditLogRepository(dataDirectory));
        services.AddSingleton<INotificationRepository>(_ => new JsonNotificationRepository(dataDirectory));
        if (enableSimulatedIdentityVerification)
        {
            services.AddSingleton<IIdentityVerificationService>(
                _ => new SimulatedIdentityVerificationService(moneyOrderPolicy.DemoVerificationSuccessPrefix));
        }
        else
        {
            services.AddSingleton<IIdentityVerificationService, UnconfiguredIdentityVerificationService>();
        }
        services.AddScoped<IApplicationEventHandler, CounterTransactionAuditHandler>();
        services.AddScoped<IApplicationEventHandler, CounterTransactionReversedAuditHandler>();
        services.AddScoped<IApplicationEventHandler, MoneyOrderAuditHandler>();
        services.AddScoped<IApplicationEventHandler, MoneyOrderRefundAuditHandler>();
        services.AddScoped<IApplicationEventHandler, IdentityVerificationAuditHandler>();
        services.AddScoped<IApplicationEventHandler, OperationalNotificationHandler>();
        services.AddScoped<IApplicationEventPublisher, ApplicationEventPublisher>();
        services.AddSingleton<IStaffPasswordService, AspNetStaffPasswordService>();
        services.AddSingleton(serviceProvider => new DemoDataSeeder(
            serviceProvider.GetRequiredService<IBranchRepository>(),
            serviceProvider.GetRequiredService<IStaffRepository>(),
            serviceProvider.GetRequiredService<IProductRepository>(),
            serviceProvider.GetRequiredService<IInventoryRepository>(),
            serviceProvider.GetRequiredService<ICounterTransactionRepository>(),
            serviceProvider.GetRequiredService<IMoneyOrderRepository>(),
            serviceProvider.GetRequiredService<IIdentityVerificationRepository>(),
            serviceProvider.GetRequiredService<IShippingZoneRepository>(),
            serviceProvider.GetRequiredService<IShippingRateRepository>(),
            serviceProvider.GetRequiredService<ICounterTerminalRepository>(),
            serviceProvider.GetRequiredService<IStaffPasswordService>(),
            moneyOrderPolicy,
            demoPassword,
            serviceProvider.GetRequiredService<IApplicationMutationCoordinator>()));

        return services;
    }
}
