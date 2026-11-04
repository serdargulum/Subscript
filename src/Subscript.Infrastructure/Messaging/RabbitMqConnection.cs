using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Subscript.Infrastructure.Messaging;

/// <summary>
/// Holds a single RabbitMQ connection for the process and reconnects on demand.
/// </summary>
public sealed class RabbitMqConnection : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _connectionString;
    private readonly ILogger<RabbitMqConnection> _logger;
    private IConnection? _connection;

    public RabbitMqConnection(
        string connectionString,
        ILogger<RabbitMqConnection> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }

            var factory = new ConnectionFactory
            {
                Uri = new Uri(_connectionString),
                AutomaticRecoveryEnabled = true
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _logger.LogInformation("Connected to RabbitMQ at {Host}", factory.HostName);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        return await connection.CreateChannelAsync(cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }

        _gate.Dispose();
    }
}
