namespace Infrastructure.Data.SqlServer.OutboxTransactionEntry.Models;

public sealed record OutboxTransactionEntryModel(
    long Id,
    Guid CorrelationId,
    Guid TransactionId,
    Guid OperationId,
    Guid ClientId,
    decimal Amount,
    string Type,
    DateTimeOffset OccurredAt,
    string Status,
    DateTimeOffset CreatedAt);
