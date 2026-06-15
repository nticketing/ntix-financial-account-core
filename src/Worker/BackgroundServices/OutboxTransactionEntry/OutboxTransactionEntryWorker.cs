namespace Worker.BackgroundServices.OutboxTransactionEntry;

public sealed class OutboxTransactionEntryWorker : BackgroundService
{
    private readonly ILogger<OutboxTransactionEntryWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public OutboxTransactionEntryWorker(ILogger<OutboxTransactionEntryWorker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        throw new NotImplementedException();
    }
}
