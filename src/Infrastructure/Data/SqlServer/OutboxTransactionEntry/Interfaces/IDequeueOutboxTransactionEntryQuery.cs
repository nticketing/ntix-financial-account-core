namespace Infrastructure.Data.SqlServer.OutboxTransactionEntry.Interfaces;

public interface IDequeueOutboxTransactionEntryQuery
{
    Task<OutboxTransactionEntryDequeued?> ExecuteAsync(CancellationToken cancellationToken = default);
}
