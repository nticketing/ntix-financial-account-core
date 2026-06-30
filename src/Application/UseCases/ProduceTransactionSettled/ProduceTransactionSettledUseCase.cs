using Application.Shared.Events.TransactionSettledEvent.Models;
using Application.UseCases.Interfaces;
using Application.UseCases.ProduceTransactionSettled.Models;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.ProduceTransactionSettled;

public sealed class ProduceTransactionSettledUseCase : IUseCase<ProduceTransactionSettledUseCaseInput>
{
    private readonly ILogger<ProduceTransactionSettledUseCase> _logger;
    private readonly IProducer<string, TransactionSettledEventMessage> _producer;

    public ProduceTransactionSettledUseCase(
        ILogger<ProduceTransactionSettledUseCase> logger, 
        IProducer<string, TransactionSettledEventMessage> producer)
    {
        _logger = logger;
        _producer = producer;
    }

    private const string TOPIC_NAME = "financialaccount.core.transaction-settled";

    public async Task ExecuteAsync(ProduceTransactionSettledUseCaseInput input, CancellationToken cancellationToken = default)
    {
        var eventId = Guid.NewGuid();

        var message = new Message<string, TransactionSettledEventMessage>();
        
        message.Key = input.ClientId.ToString();
        message.Value = new TransactionSettledEventMessage(
            EventId: eventId,
            CorrelationId: input.CorrelationId,
            TransactionId: input.TransactionId,
            OperationId: input.OperationId,
            Type: input.TransactionType,
            Amount: input.Amount,
            OccurredAt: input.OccurredAt,
            PersistedAt: input.CreatedAt);

        await _producer.ProduceAsync(
            topic: TOPIC_NAME,
            message: message,
            cancellationToken: cancellationToken);

        _logger.LogInformation("[{Type}] The transaction settled has been published to topic. Input = {@Input}",
            nameof(ProduceTransactionSettledUseCase),
            new 
            {
                EventId = eventId,
                input.CorrelationId,
                input.TransactionId,
                input.OperationId,
                input.TransactionType,
                input.OccurredAt,
                Topic = TOPIC_NAME
            });
    }
}
