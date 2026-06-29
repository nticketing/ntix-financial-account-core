using Infrastructure.Data.SqlServer.ConnectionManager.Interfaces;
using Infrastructure.Data.SqlServer.UnitOfWork.Interfaces;
using System.Data;
using System.Data.Common;

namespace Infrastructure.Data.SqlServer.UnitOfWork;

public sealed class DefaultUnitOfWork : IUnitOfWork
{
    private readonly ISqlServerConnectionManager _connectionManager;

    public DefaultUnitOfWork(ISqlServerConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task<TOutput?> ExecuteUnitOfWorkAsync<TInput, TOutput>(TInput input, Func<TInput, DbTransaction, CancellationToken, Task<(bool CommitTransaction, TOutput? Output)>> execute, CancellationToken cancellationToken = default, IsolationLevel isolationLevel = IsolationLevel.RepeatableRead)
    {
        var connection = await _connectionManager.TryConnectAsync(cancellationToken);

        using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var output = await execute(input, transaction, cancellationToken);

        if (output.CommitTransaction)
        {
            await transaction.CommitAsync(cancellationToken);
            await transaction.DisposeAsync();

            return output.Output;
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken);
            await transaction.DisposeAsync();

            return output.Output;
        }
    }
}
