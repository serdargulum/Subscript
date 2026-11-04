using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Subscript.Infrastructure.Messaging;

public sealed class BillingQueuePublisher(
    RabbitMqConnection connection,
    IOptions<MessagingOptions> options,
    ILogger<BillingQueuePublisher> logger)
{
    public async Task PublishAsync(string type, string payload, CancellationToken cancellationToken)
    {
        var queueName = options.Value.QueueName;

        await using var channel = await connection.CreateChannelAsync(cancellationToken);

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var body = Encoding.UTF8.GetBytes(payload);
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            Type = type,
            MessageId = Guid.NewGuid().ToString("N"),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        logger.LogDebug("Published {MessageType} to {Queue}", type, queueName);
    }
}
