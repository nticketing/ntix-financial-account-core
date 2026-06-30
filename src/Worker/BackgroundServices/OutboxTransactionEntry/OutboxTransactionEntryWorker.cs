using Application.UseCases.Interfaces;
using Application.UseCases.ProduceTransactionSettled.Models;
using Domain.TransactionsContext.Enums;
using Infrastructure.Data.SqlServer.OutboxTransactionEntry.Interfaces;
using Worker.BackgroundServices.OutboxTransactionEntry.Configuration;

namespace Worker.BackgroundServices.OutboxTransactionEntry;

public sealed class OutboxTransactionEntryWorker : BackgroundService
{
    private readonly ILogger<OutboxTransactionEntryWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly OutboxTransactionEntryWorkerConfiguration _configuration;

    public OutboxTransactionEntryWorker(
        ILogger<OutboxTransactionEntryWorker> logger,
        IServiceProvider serviceProvider,
        OutboxTransactionEntryWorkerConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
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
                    await Task.Delay(_configuration.DelayBetweenIterationsInMilliseconds, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Type}] Unhandled exception. Retrying after delay.", nameof(OutboxTransactionEntryWorker));

                await Task.Delay(_configuration.DelayBetweenIterationsInMilliseconds, stoppingToken);
            }
        }

        _logger.LogInformation("[{Type}] Worker stopped.", nameof(OutboxTransactionEntryWorker));
    }

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();

        var dequeueQuery = scope.ServiceProvider.GetRequiredService<IDequeueOutboxTransactionEntryRepository>();

        var entry = await dequeueQuery.ExecuteAsync(cancellationToken);
        if (entry is null)
            return false;

        var dequeueAt = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "[{Type}] Dequeued. Input = {@Input}",
            nameof(OutboxTransactionEntryWorker),
            new 
            { 
                entry.Id, 
                entry.CorrelationId,
                entry.TransactionId,
                entry.ClientId, 
                entry.OperationId,
                entry.Type,
                entry.OccurredAt,
                ElapsedTransactionToDequeueMs = (dequeueAt - entry.OccurredAt).TotalMilliseconds
            });

        var useCase = scope.ServiceProvider.GetRequiredService<IUseCase<ProduceTransactionSettledUseCaseInput>>();

        var input = new ProduceTransactionSettledUseCaseInput(
            CorrelationId: entry.CorrelationId,
            TransactionId: entry.TransactionId,
            ClientId: entry.ClientId,
            OperationId: entry.OperationId,
            TransactionType: Enum.Parse<TransactionType>(entry.Type),
            Amount: entry.Amount,
            OccurredAt: entry.OccurredAt,
            CreatedAt: entry.CreatedAt);

        await useCase.ExecuteAsync(input, cancellationToken);

        return true;
    }
}
