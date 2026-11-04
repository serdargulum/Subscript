using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Subscript.Domain.Common;
using Subscript.Infrastructure.Persistence;

namespace Subscript.Infrastructure.Messaging;

/// <summary>
/// Periodically enqueues a billing run through the outbox. At most one pending
/// command is kept so a stalled broker does not accumulate duplicate work.
/// </summary>
public sealed class BillingScheduler(
    IServiceScopeFactory scopeFactory,
    IOptions<BillingOptions> options,
    ILogger<BillingScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.SchedulerIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnqueueBillingRunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not enqueue a billing run; retrying in {IntervalSeconds}s.", interval.TotalSeconds);
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

    private async Task EnqueueBillingRunAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SubscriptDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var alreadyQueued = await db.OutboxMessages.AnyAsync(
            x => x.ProcessedAt == null && x.Type == OutboxMessageTypes.BillingDue,
            cancellationToken);

        if (alreadyQueued)
        {
            return;
        }

        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = OutboxMessageTypes.BillingDue,
            Payload = JsonSerializer.Serialize(new
            {
                requestedAt = clock.UtcNow,
                source = "scheduler"
            }),
            OccurredAt = clock.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Queued billing run.");
    }
}
