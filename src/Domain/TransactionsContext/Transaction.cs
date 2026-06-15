using Domain.TransactionsContext.Enums;

namespace Domain.TransactionsContext;

public sealed record Transaction(Guid TransactionId, Guid CorrelationId, Guid OperationId, Guid ClientId, decimal Amount, TransactionType Type, DateTime OccurredAt);
