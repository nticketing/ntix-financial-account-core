using Domain.TransactionsContext.Enums;
using System.Text.Json.Serialization;

namespace Application.Shared.Events.TransactionSettledEvent.Models;

public sealed record TransactionSettledEventMessage(Guid EventId, Guid CorrelationId, Guid TransactionId, Guid OperationId, [property: JsonConverter(typeof(JsonStringEnumConverter))] TransactionType Type, decimal Amount, DateTimeOffset OccurredAt, DateTimeOffset PersistedAt)
{
}
