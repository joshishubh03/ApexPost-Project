using ApexPost.Application.Courier;
using ApexPost.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApexPost.Infrastructure;

public static class CourierDependencyInjection
{
    /// <summary>
    /// Week 4 courier shipments (additive): repository + application service on top of the Week 3 SQL Server
    /// composition (<see cref="DependencyInjection.AddApexPostSqlServer"/> must also be called).
    /// </summary>
    public static IServiceCollection AddApexPostCourierShipments(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICourierShipmentRepository, EfCourierShipmentRepository>();
        services.AddScoped<CourierShipmentService>();
        return services;
    }
}
