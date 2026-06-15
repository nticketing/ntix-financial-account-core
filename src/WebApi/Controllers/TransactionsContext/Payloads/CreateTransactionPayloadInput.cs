using Domain.TransactionsContext.Enums;

namespace WebApi.Controllers.TransactionsContext.Payloads;

public sealed record CreateTransactionPayloadInput(decimal Amount, DateTime OccurredAt, TransactionType Type);
