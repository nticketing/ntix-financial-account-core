using Domain.TransactionsContext.Enums;

namespace Domain.TransactionsContext;

public record Transaction
{
    public Guid TransactionId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid OperationId { get; set; }
    public Guid ClientId { get; set; }
    public decimal Amount { get; set; }
    public TransactionType Type { get; set;  }
    public DateTime OccurredAt { get; set; }
    public DateTime PersistedAt { get; set; }
}
