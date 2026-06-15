using Domain.TransactionsContext.Enums;

namespace Application.UseCases.CreateTransactions.Models;

public sealed record CreateTransactionUseCaseInput(Guid CorrelationId, Guid ClientId, Guid OperationId, decimal Amount, TransactionType Type, DateTime OccurredAt);
