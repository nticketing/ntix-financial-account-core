using Infrastructure.Data.SqlServer.ConnectionManager.Interfaces;
using Infrastructure.Data.SqlServer.OutboxTransactionEntry.Interfaces;
using Microsoft.Data.SqlClient;

namespace Infrastructure.Data.SqlServer.OutboxTransactionEntry;

public sealed class DequeueOutboxTransactionEntryQuery : IDequeueOutboxTransactionEntryQuery
{
    private readonly ISqlServerConnectionManager _connectionManager;

    public DequeueOutboxTransactionEntryQuery(ISqlServerConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task<OutboxTransactionEntryDequeued?> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _connectionManager.TryConnectAsync(cancellationToken);

        // Columns: Id(0) CorrelationId(1) TransactionId(2) OperationId(3) ClientId(4) Amount(5) Type(6) OccurredAt(7) Status(8)
        using var command = new SqlCommand("EXEC [financial_truth].[sp_dequeue_transaction_outbox]", connection);
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
            Status:        reader.GetString(8));
    }
}
