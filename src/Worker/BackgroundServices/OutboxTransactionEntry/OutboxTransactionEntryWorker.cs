using Infrastructure.Data.SqlServer.OutboxTransactionEntry.Interfaces;

namespace Worker.BackgroundServices.OutboxTransactionEntry;

public sealed class OutboxTransactionEntryWorker : BackgroundService
{
    private readonly ILogger<OutboxTransactionEntryWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly OutboxTransactionEntryWorkerOptions _options;

    public OutboxTransactionEntryWorker(
        ILogger<OutboxTransactionEntryWorker> logger,
        IServiceProvider serviceProvider,
        OutboxTransactionEntryWorkerOptions options)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[{Type}] Worker started.", nameof(OutboxTransactionEntryWorker));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessNextAsync(stoppingToken);
                
                if (!processed) 
                    await Task.Delay(_options.DelayBetweenIterationsInMilliseconds, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Type}] Unhandled exception. Retrying after delay.", nameof(OutboxTransactionEntryWorker));
                await Task.Delay(_options.DelayBetweenIterationsInMilliseconds, stoppingToken);
            }
        }

        _logger.LogInformation("[{Type}] Worker stopped.", nameof(OutboxTransactionEntryWorker));
    }

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var dequeueQuery = scope.ServiceProvider.GetRequiredService<IDequeueOutboxTransactionEntryQuery>();

        var entry = await dequeueQuery.ExecuteAsync(cancellationToken);
        if (entry is null)
            return false;

        _logger.LogInformation(
            "[{Type}] Dequeued. Id={Id} TransactionId={TransactionId} ClientId={ClientId} Type={Type} Amount={Amount}",
            nameof(OutboxTransactionEntryWorker),
            entry.Id, entry.TransactionId, entry.ClientId, entry.Type, entry.Amount);

        // TODO: publicar evento para broker

        return true;
    }
}
