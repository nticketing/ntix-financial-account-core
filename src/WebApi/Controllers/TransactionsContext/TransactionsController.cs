using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using WebApi.Controllers.TransactionsContext.Payloads;

namespace WebApi.Controllers.TransactionsContext;

[Route("/api/v1/transactions")]
[ApiController]
public sealed class TransactionsController : ControllerBase
{
    [HttpPost]
    [Route("{operationId}")]
    [AllowAnonymous]
    public async Task<IActionResult> HttpPostCreateTransactionsAsync(
        [FromHeader] Guid clientId,
        [FromHeader] Guid correlationId,
        [FromRoute] Guid operationId,
        [FromBody] CreateTransactionPayloadInput input,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
