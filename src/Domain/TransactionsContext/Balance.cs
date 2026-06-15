namespace Domain.TransactionsContext;

public sealed class Balance
{
    public Guid ClientId { get; set; }
    public decimal CurrentBalance { get; set;  }
    public long Version { get; set; }
}
