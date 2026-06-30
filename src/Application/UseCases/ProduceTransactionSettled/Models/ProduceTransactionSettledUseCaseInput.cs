using Domain.TransactionsContext.Enums;

namespace Application.UseCases.ProduceTransactionSettled.Models;

public sealed record ProduceTransactionSettledUseCaseInput(Guid CorrelationId, Guid TransactionId, Guid ClientId, Guid OperationId, TransactionType TransactionType, decimal Amount, DateTimeOffset OccurredAt, DateTimeOffset CreatedAt);
