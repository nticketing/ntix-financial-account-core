using Application.UseCases.CreateTransactions.Models;

namespace Application.UseCases.Interfaces;

public interface IUseCase<TInput>
{
    public Task ExecuteAsync(TInput input, CancellationToken cancellationToken = default);
}
