using Infrastructure.Data.SqlServer.OutboxTransactionEntry.Models;

namespace Infrastructure.Data.SqlServer.OutboxTransactionEntry.Interfaces;

public interface IDequeueOutboxTransactionEntryRepository
{
    Task<OutboxTransactionEntryDequeued?> ExecuteAsync(CancellationToken cancellationToken = default);
}
