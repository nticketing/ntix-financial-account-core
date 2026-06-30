using Infrastructure.Data.SqlServer.ConnectionManager.Interfaces;
using Infrastructure.Data.SqlServer.OutboxTransactionEntry.Interfaces;
using Infrastructure.Data.SqlServer.OutboxTransactionEntry.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Infrastructure.Data.SqlServer.OutboxTransactionEntry;

public sealed class DequeueOutboxTransactionEntryRepository : IDequeueOutboxTransactionEntryRepository
{
    private readonly ISqlServerConnectionManager _connectionManager;

    public DequeueOutboxTransactionEntryRepository(ISqlServerConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task UpdateOutboxTransactionEntryToProcessedAsync(
        long entryId,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionManager.TryConnectAsync(cancellationToken);

        const string SQL = @"
            UPDATE [financial_truth].[transactions_outbox] 
            SET [Status] = 'PROCESSED',
                [ProcessedAt] = SYSUTCDATETIME() 
            WHERE Id = @EntryId
        ";

        using var command = new SqlCommand(SQL, connection);
        command.Parameters.Add(new SqlParameter("EntryId", entryId));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }


    public async Task<IEnumerable<OutboxTransactionEntryDequeued>?> ExecuteAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        const string procedureName = "[financial_truth].[sp_dequeue_transaction_outbox]";


        var connection = await _connectionManager.TryConnectAsync(cancellationToken);

        using var command = new SqlCommand(procedureName, connection);

        command.Parameters.Add(new SqlParameter("@BatchSize", SqlDbType.Int)
        {
            Value = batchSize
        });

        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var dequeuedEntries = new List<OutboxTransactionEntryDequeued>(batchSize);

        while (await reader.ReadAsync(cancellationToken))
        {
            dequeuedEntries.Add(new OutboxTransactionEntryDequeued(
                Id: reader.GetInt64(0),
                CorrelationId: reader.GetGuid(1),
                TransactionId: reader.GetGuid(2),
                OperationId: reader.GetGuid(3),
                ClientId: reader.GetGuid(4),
                Amount: reader.GetDecimal(5),
                Type: reader.GetString(6),
                OccurredAt: reader.GetDateTime(7),
                Status: reader.GetString(8),
                CreatedAt: DateTimeOffset.UtcNow
            ));
        }

        return dequeuedEntries;
    }
}
