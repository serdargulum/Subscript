using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Subscript.Application.Abstractions;
using Subscript.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Subscript.IntegrationTests.Infrastructure;

public sealed class SubscriptApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("subscript")
        .WithUsername("subscript")
        .WithPassword("subscript")
        .Build();

    public string ApiKey { get; } = "integration-test-api-key";

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<SubscriptDbContext>));
            services.RemoveAll(typeof(SubscriptDbContext));

            services.AddDbContext<SubscriptDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));

            services.AddScoped<ISubscriptDbContext>(sp =>
                sp.GetRequiredService<SubscriptDbContext>());
        });

        builder.UseSetting("Authentication:ApiKey", ApiKey);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Database:SeedOnStartup", "false");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());

        // Background messaging is exercised separately; API tests must not need a broker.
        builder.UseSetting("Messaging:Enabled", "false");
        builder.UseSetting("Billing:SchedulerEnabled", "false");
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Remove("X-Api-Key");
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        return client;
    }
}
