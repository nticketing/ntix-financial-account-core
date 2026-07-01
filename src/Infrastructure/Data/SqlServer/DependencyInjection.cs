using Infrastructure.Data.SqlServer.ConnectionManager;
using Infrastructure.Data.SqlServer.ConnectionManager.Interfaces;
using Infrastructure.Data.SqlServer.OutboxTransactionEntry;
using Infrastructure.Data.SqlServer.OutboxTransactionEntry.Interfaces;
using Infrastructure.Data.SqlServer.UnitOfWork;
using Infrastructure.Data.SqlServer.UnitOfWork.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Infrastructure.Data.SqlServer;

public static class DependencyInjection
{
    public static IServiceCollection AddSqlServerManagedConnectionConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddTransient<ISqlServerConnectionManager, SqlServerConnectionManager>();

        return services;
    }

    public static IServiceCollection AddUnitOfWorkConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddScoped<IUnitOfWork, DefaultUnitOfWork>();

        return services;
    }

    public static IServiceCollection AddOutboxQueryConfiguration(this IServiceCollection services)
    {
        services.TryAddScoped<IOutboxTransactionEntryRepository, OutboxTransactionEntryRepository>();

        return services;
    }
}
