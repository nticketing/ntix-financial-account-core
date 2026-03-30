using Domain.TransactionsContext.Enums;

namespace Domain.TransactionsContext;

public sealed class OutboxMessage
{
    public Guid EventId { get; set; }
    public Guid TransactionId { get; set; }
    public Guid CorrelationId { get; set; }
    public OutboxStatus Status { get; set;  }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
