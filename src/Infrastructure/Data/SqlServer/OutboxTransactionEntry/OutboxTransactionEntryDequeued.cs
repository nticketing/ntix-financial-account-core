namespace Infrastructure.Data.SqlServer.OutboxTransactionEntry;

public sealed record OutboxTransactionEntryDequeued(
    long Id,
    Guid CorrelationId,
    Guid TransactionId,
    Guid OperationId,
    Guid ClientId,
    decimal Amount,
    string Type,
    DateTime OccurredAt,
    string Status);
