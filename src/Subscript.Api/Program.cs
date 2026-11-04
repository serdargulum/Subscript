using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Serilog;
using Subscript.Api.Authentication;
using Subscript.Api.Middleware;
using Subscript.Application;
using Subscript.Domain.Common;
using Subscript.Infrastructure;
using Subscript.Infrastructure.Messaging;
using Subscript.Infrastructure.Persistence;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services
        .AddAuthentication(ApiKeyAuthenticationOptions.DefaultScheme)
        .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationOptions.DefaultScheme,
            options =>
            {
                options.ApiKey = builder.Configuration["Authentication:ApiKey"] ?? string.Empty;
            });

    builder.Services.AddAuthorization();
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter());
        });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Subscript API",
            Version = "v1",
            Description = "Subscription management API for SaaS businesses."
        });

        options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
        {
            Description = "API key used by the Next.js BFF. Header: X-Api-Key",
            Name = ApiKeyAuthenticationOptions.HeaderName,
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey
        });

        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("ApiKey", document)] = []
        });
    });

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    var healthChecks = builder.Services.AddHealthChecks();
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        healthChecks.AddNpgSql(connectionString, name: "postgres");
    }

    var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMq");
    var messagingEnabled = builder.Configuration.GetValue("Messaging:Enabled", true)
        && !string.IsNullOrWhiteSpace(rabbitMqConnectionString);
    if (messagingEnabled)
    {
        healthChecks.AddRabbitMQ(
            serviceProvider => serviceProvider
                .GetRequiredService<RabbitMqConnection>()
                .GetConnectionAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult(),
            name: "rabbitmq");
    }

    builder.Services.Configure<HostOptions>(options =>
    {
        options.ShutdownTimeout = TimeSpan.FromSeconds(
            builder.Configuration.GetValue("Host:ShutdownTimeoutSeconds", 30));
    });

    var app = builder.Build();

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

    var applyMigrations = app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false);
    var seedOnStartup = app.Configuration.GetValue("Database:SeedOnStartup", false);

    if (applyMigrations || seedOnStartup)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SubscriptDbContext>();

        if (applyMigrations)
        {
            await db.Database.MigrateAsync();
        }

        if (seedOnStartup)
        {
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            await DatabaseSeeder.SeedAsync(db, clock, app.Logger);
        }
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
