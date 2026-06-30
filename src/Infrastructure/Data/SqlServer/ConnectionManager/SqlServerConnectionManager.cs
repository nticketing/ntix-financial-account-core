using Infrastructure.Data.SqlServer.Configuration;
using Infrastructure.Data.SqlServer.ConnectionManager.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;
namespace Infrastructure.Data.SqlServer.ConnectionManager;

public sealed class SqlServerConnectionManager : ISqlServerConnectionManager
{
    private readonly ILogger<SqlServerConnectionManager> _logger;
    private readonly SqlServerConfiguration _configuration;

    public SqlServerConnectionManager(ILogger<SqlServerConnectionManager> logger, SqlServerConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    private SqlConnection? _connection;

    private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(_connectionMaxAccessLockCount);
    private const int _connectionMaxAccessLockCount = 1;

    public async Task<SqlConnection> TryConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_connection != null)
        {
            if (_connection.State != ConnectionState.Open)
                await _connection.OpenAsync(cancellationToken);

            return _connection;
        }

        await _connectionLock.WaitAsync(cancellationToken);

        var sqlConnection = new SqlConnection(_configuration.ConnectionString);

        try
        {
            _connection?.Dispose();
            _connection = null;

            _connection = sqlConnection;
            
            await _connection.OpenAsync(cancellationToken);

            return _connection;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(
                exception: ex,
                message: "[{Type}] Got critical exception handling sql server openning connection. Input = {@Input}",
                nameof(SqlServerConnectionManager),
                new 
                {
                    sqlConnection.ClientConnectionId,
                    sqlConnection.CommandTimeout,
                    sqlConnection.Database,
                    sqlConnection.ServerProcessId
                });

            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }
}
