using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Subscript.Domain.Common;
using Subscript.Infrastructure.Persistence;

namespace Subscript.Infrastructure.Messaging;

/// <summary>
/// Publishes durable outbox records to RabbitMQ. A record is marked processed only
/// after the broker accepted the message, so a crash cannot silently drop work.
/// </summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    BillingQueuePublisher publisher,
    IOptions<MessagingOptions> options,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.OutboxPollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Outbox dispatch failed; retrying in {IntervalSeconds}s.", interval.TotalSeconds);
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SubscriptDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var messages = await db.OutboxMessages
            .Where(x => x.ProcessedAt == null)
            .OrderBy(x => x.OccurredAt)
            .Take(Math.Max(1, options.Value.OutboxBatchSize))
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(message.Type, message.Payload, cancellationToken);
                message.ProcessedAt = clock.UtcNow;
                message.Error = null;
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                message.Error = Truncate(ex.Message);
                await db.SaveChangesAsync(cancellationToken);
                logger.LogWarning(
                    ex,
                    "Could not publish outbox message {MessageId} of type {MessageType}.",
                    message.Id,
                    message.Type);
                break;
            }
        }
    }

    private static string Truncate(string value) =>
        value.Length <= 2000 ? value : value[..2000];
}
