using Infrastructure.Data.SqlServer.OutboxTransactionEntry.Models;

namespace Infrastructure.Data.SqlServer.OutboxTransactionEntry.Interfaces;

public interface IOutboxTransactionEntryRepository
{
    Task<IEnumerable<OutboxTransactionEntryModel>?> ExecuteAsync(int batchSize = 100, CancellationToken cancellationToken = default);
    Task UpdateOutboxTransactionEntryToProcessedAsync(long entryId, CancellationToken cancellationToken = default);
}
