namespace Subscript.Infrastructure.Messaging;

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public bool Enabled { get; set; } = true;
    public string QueueName { get; set; } = "subscript.billing";
    public ushort PrefetchCount { get; set; } = 1;
    public int OutboxPollIntervalSeconds { get; set; } = 5;
    public int OutboxBatchSize { get; set; } = 50;
}

public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    public bool SchedulerEnabled { get; set; } = true;
    public int SchedulerIntervalSeconds { get; set; } = 60;
}

public static class OutboxMessageTypes
{
    public const string BillingDue = "BillingDue";
}
