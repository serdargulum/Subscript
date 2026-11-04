using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Subscript.Application.Billing;

namespace Subscript.Infrastructure.Messaging;

/// <summary>
/// Consumes billing commands and processes due subscriptions. The billing service
/// is idempotent, so a redelivered message cannot create a duplicate invoice.
/// </summary>
public sealed class BillingWorker(
    RabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    IOptions<MessagingOptions> options,
    ILogger<BillingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueName = options.Value.QueueName;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var channel = await connection.CreateChannelAsync(stoppingToken);

                await channel.QueueDeclareAsync(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await channel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: options.Value.PrefetchCount,
                    global: false,
                    cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, args) =>
                {
                    try
                    {
                        using var scope = scopeFactory.CreateScope();
                        var billing = scope.ServiceProvider.GetRequiredService<BillingService>();
                        var processed = await billing.ProcessDueSubscriptionsAsync(stoppingToken);

                        logger.LogInformation(
                            "Billing run {MessageId} processed {ProcessedCount} subscription(s).",
                            args.BasicProperties.MessageId,
                            processed);

                        await channel.BasicAckAsync(args.DeliveryTag, multiple: false, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Billing message processing failed.");
                        await channel.BasicNackAsync(
                            args.DeliveryTag,
                            multiple: false,
                            requeue: false,
                            stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: queueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                logger.LogInformation("Billing worker listening on {Queue}.", queueName);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Billing worker could not reach RabbitMQ; retrying in {DelaySeconds}s.",
                    ReconnectDelay.TotalSeconds);

                try
                {
                    await Task.Delay(ReconnectDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
