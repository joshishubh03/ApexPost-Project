using ApexPost.Application.Abstractions;
using ApexPost.Application.Events;
using ApexPost.Application.Models;
using ApexPost.Application.Services;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Events;
using ApexPost.Infrastructure.Persistence.Repositories;
using ApexPost.Infrastructure.Security;
using ApexPost.Infrastructure.Seed;
using ApexPost.Application.Queries;
using ApexPost.Infrastructure.Persistence.Commands;
using ApexPost.Infrastructure.Persistence.Queries;
using ApexPost.Infrastructure.Persistence.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// SQL Server composition: one scoped DbContext per request, shared by the scoped repositories, the unit of
    /// work, the query services and the commands. Nothing here is a singleton except the stateless interceptor.
    /// </summary>
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

        services.AddSingleton<ISaveChangesInterceptor, AuditColumnsInterceptor>();
        services.AddDbContext<ApexPostDbContext>((provider, options) => options
            .UseSqlServer(
                connectionString,
                sql => sql
                    .CommandTimeout(commandTimeoutSeconds)
                    // Transient faults (including deadlock victim 1205) retry the whole unit of work.
                    .EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)
                    .MigrationsAssembly(typeof(ApexPostDbContext).Assembly.GetName().Name))
            .AddInterceptors(provider.GetServices<ISaveChangesInterceptor>()));

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IBranchRepository, EfBranchRepository>();
        services.AddScoped<ICounterTerminalRepository, EfCounterTerminalRepository>();
        services.AddScoped<IStaffRepository, EfStaffRepository>();
        services.AddScoped<IProductRepository, EfProductRepository>();
        services.AddScoped<IInventoryRepository, EfInventoryRepository>();
        services.AddScoped<IShippingZoneRepository, EfShippingZoneRepository>();
        services.AddScoped<IShippingRateRepository, EfShippingRateRepository>();
        services.AddScoped<ICounterTransactionRepository, EfCounterTransactionRepository>();
        services.AddScoped<IMoneyOrderRepository, EfMoneyOrderRepository>();
        services.AddScoped<IIdentityVerificationRepository, EfIdentityVerificationRepository>();
        services.AddScoped<IAuditLogRepository, EfAuditLogRepository>();
        services.AddScoped<INotificationRepository, EfNotificationRepository>();
        services.AddScoped<IAuditStore, EfAuditStore>();
        services.AddScoped<IStoredProcedureService, SqlServerStoredProcedureService>();
        services.AddScoped<EfReportingQueries>();
        services.AddScoped<IReportingQueries>(provider => provider.GetRequiredService<EfReportingQueries>());
        services.AddScoped<IPriceRevisionService, EfPriceRevisionService>();
        services.AddScoped<IStockAdjustmentCommand, SqlStockAdjustmentCommand>();
        return services;
    }

    /// <summary>
    /// Persistence-independent infrastructure: identity verification, event handlers, password hashing and the
    /// demo-data seeder. Persistence itself is registered by <see cref="AddApexPostSqlServer"/>.
    /// </summary>
    public static IServiceCollection AddApexPostInfrastructure(
        this IServiceCollection services,
        string demoPassword,
        MoneyOrderPolicy moneyOrderPolicy,
        bool enableSimulatedIdentityVerification)
    {
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
        services.AddScoped(serviceProvider => new DemoDataSeeder(
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
            serviceProvider.GetRequiredService<IUnitOfWork>()));

        return services;
    }
}
