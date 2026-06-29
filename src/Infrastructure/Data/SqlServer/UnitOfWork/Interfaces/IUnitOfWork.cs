using System.Data;
using System.Data.Common;

namespace Infrastructure.Data.SqlServer.UnitOfWork.Interfaces;

public interface IUnitOfWork
{
    public Task<TOutput?> ExecuteUnitOfWorkAsync<TInput, TOutput>(TInput input, Func<TInput, DbTransaction, CancellationToken, Task<(bool CommitTransaction, TOutput? Output)>> execute, CancellationToken cancellationToken = default, IsolationLevel isolationLevel = IsolationLevel.RepeatableRead);
}
