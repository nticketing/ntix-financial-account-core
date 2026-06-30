using Application.Shared.Events.TransactionSettledEvent.Models;
using Application.UseCases.Interfaces;
using Application.UseCases.ProduceTransactionSettled.Models;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Application.UseCases.ProduceTransactionSettled;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProduceTransactionSettledConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var producerConfig = configuration.GetRequiredSection("Events:TransactionSettled:ProducerConfig").Get<ProducerConfig>();

        services.AddScoped((serviceProvider) => new ProducerBuilder<string, TransactionSettledEventMessage>(producerConfig)
            .SetKeySerializer(Serializers.Utf8)
            .SetValueSerializer(JsonKafkaSerializer<TransactionSettledEventMessage>.Build()).Build());

        services.AddScoped<IUseCase<ProduceTransactionSettledUseCaseInput>, ProduceTransactionSettledUseCase>();

        return services;
    }
}

public sealed class JsonKafkaSerializer<T> : ISerializer<T>
{
    public static JsonKafkaSerializer<T> Build()
        => new JsonKafkaSerializer<T>();

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public byte[] Serialize(T data, SerializationContext context)
    {
        if (data is null)
            return null!;

        return JsonSerializer.SerializeToUtf8Bytes(data, Options);
    }
}