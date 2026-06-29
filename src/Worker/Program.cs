using Infrastructure.Data.SqlServer;
using Infrastructure.Data.SqlServer.Configuration;
using Worker.BackgroundServices.OutboxTransactionEntry;

namespace Worker;

public sealed class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddSingleton(new SqlServerConfiguration(
            builder.Configuration.GetConnectionString("SqlServer")
                ?? throw new InvalidOperationException("Missing 'SqlServer' connection string in configuration.")));

        builder.Services.AddSqlServerManagedConnectionConfiguration(builder.Configuration);
        builder.Services.AddUnitOfWorkConfiguration(builder.Configuration);
        builder.Services.AddOutboxQueryConfiguration();

        var workerOptions = builder.Configuration
            .GetSection("OutboxTransactionEntryWorker")
            .Get<OutboxTransactionEntryWorkerOptions>()
            ?? new OutboxTransactionEntryWorkerOptions(DelayBetweenIterationsInMilliseconds: 5000);
        builder.Services.AddSingleton(workerOptions);

        builder.Services.AddHostedService<OutboxTransactionEntryWorker>();

        var host = builder.Build();
        host.Run();
    }
}
