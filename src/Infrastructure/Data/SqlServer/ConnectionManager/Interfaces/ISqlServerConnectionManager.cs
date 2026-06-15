using Microsoft.Data.SqlClient;

namespace Infrastructure.Data.SqlServer.ConnectionManager.Interfaces;

public interface ISqlServerConnectionManager
{
    public Task<SqlConnection> TryConnectAsync(CancellationToken cancellationToken = default);
}
