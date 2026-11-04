using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Subscript.Application.Abstractions;
using Subscript.Domain.Common;
using Subscript.Infrastructure.Messaging;
using Subscript.Infrastructure.Persistence;
using Subscript.Infrastructure.Time;

namespace Subscript.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();

        services.AddDbContext<SubscriptDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<ISubscriptDbContext>(sp => sp.GetRequiredService<SubscriptDbContext>());

        services.Configure<BillingOptions>(configuration.GetSection(BillingOptions.SectionName));

        var rabbitMqConnectionString = configuration.GetConnectionString("RabbitMq");
        var messagingEnabled = configuration.GetValue($"{MessagingOptions.SectionName}:Enabled", true)
            && !string.IsNullOrWhiteSpace(rabbitMqConnectionString);

        if (messagingEnabled)
        {
            services.Configure<MessagingOptions>(configuration.GetSection(MessagingOptions.SectionName));

            services.AddSingleton(sp => new RabbitMqConnection(
                rabbitMqConnectionString!,
                sp.GetRequiredService<ILogger<RabbitMqConnection>>()));

            services.AddSingleton<BillingQueuePublisher>();
            services.AddHostedService<OutboxDispatcher>();
            services.AddHostedService<BillingWorker>();

            if (configuration.GetValue($"{BillingOptions.SectionName}:SchedulerEnabled", true))
            {
                services.AddHostedService<BillingScheduler>();
            }
        }

        return services;
    }
}
