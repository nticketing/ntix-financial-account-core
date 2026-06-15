using Domain.TransactionsContext.Enums;

namespace Domain.TransactionsContext;

public sealed record OutboxMessage(Guid EventId, Guid TransactionId, Guid CorrelationId, OutboxStatus Status, DateTime CreatedAt, DateTime? ProcessedAt);
