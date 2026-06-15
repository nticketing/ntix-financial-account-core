using Application.UseCases.CreateTransactions.Models;
using Application.UseCases.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.CreateTransaction;

public sealed class CreateTransactionUseCase : IUseCase<CreateTransactionUseCaseInput>
{
    private readonly ILogger<CreateTransactionUseCase> _logger;

    public Task ExecuteAsync(CreateTransactionUseCaseInput input, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
