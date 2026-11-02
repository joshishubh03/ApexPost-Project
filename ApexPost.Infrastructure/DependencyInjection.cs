using ApexPost.Application.Abstractions;
using ApexPost.Application.Events;
using ApexPost.Application.Models;
using ApexPost.Application.Services;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Events;
using ApexPost.Infrastructure.Persistence.Repositories;
using ApexPost.Infrastructure.Repositories;
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
        services.AddScoped<IApplicationMutationCoordinator, UnitOfWorkMutationCoordinator>();
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

    public static IServiceCollection AddApexPostInfrastructure(
        this IServiceCollection services,
        string dataDirectory,
        string demoPassword,
        MoneyOrderPolicy moneyOrderPolicy,
        bool enableSimulatedIdentityVerification,
        string? connectionString = null,
        bool useSqlServer = false)
    {
        if (useSqlServer)
        {
            // SQL Server registrations are intentionally centralized in AddApexPostSqlServer.
            // Do not register a second DbContext or SQL repositories here.
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "SQL Server persistence is enabled, but no connection string was supplied.");
            }
        }
        else
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
            services.AddScoped<IAuditStore, RepositoryAuditStore>();
        }

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
            serviceProvider.GetRequiredService<IApplicationMutationCoordinator>(),
            serviceProvider.GetService<IUnitOfWork>() ?? new NoOpUnitOfWork()));

        return services;
    }
}
