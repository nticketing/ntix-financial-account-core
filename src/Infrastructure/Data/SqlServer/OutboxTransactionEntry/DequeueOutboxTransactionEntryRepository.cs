using Infrastructure.Data.SqlServer.ConnectionManager.Interfaces;
using Infrastructure.Data.SqlServer.OutboxTransactionEntry.Interfaces;
using Infrastructure.Data.SqlServer.OutboxTransactionEntry.Models;
using Microsoft.Data.SqlClient;

namespace Infrastructure.Data.SqlServer.OutboxTransactionEntry;

public sealed class DequeueOutboxTransactionEntryRepository : IDequeueOutboxTransactionEntryRepository
{
    private readonly ISqlServerConnectionManager _connectionManager;

    public DequeueOutboxTransactionEntryRepository(ISqlServerConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task<OutboxTransactionEntryDequeued?> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        const string SQL = @"
            EXEC [financial_truth].[sp_dequeue_transaction_outbox];
        ";

        var connection = await _connectionManager.TryConnectAsync(cancellationToken);

        using var command = new SqlCommand(SQL, connection);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new OutboxTransactionEntryDequeued(
            Id:            reader.GetInt64(0),
            CorrelationId: reader.GetGuid(1),
            TransactionId: reader.GetGuid(2),
            OperationId:   reader.GetGuid(3),
            ClientId:      reader.GetGuid(4),
            Amount:        reader.GetDecimal(5),
            Type:          reader.GetString(6),
            OccurredAt:    reader.GetDateTime(7),
            Status:        reader.GetString(8),
            CreatedAt:     DateTimeOffset.UtcNow);
    }
}
