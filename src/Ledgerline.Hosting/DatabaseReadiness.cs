using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Ledgerline.Hosting;

/// <summary>
/// Waits for PostgreSQL before Marten applies its schema. On Kubernetes nothing orders startups: a service that
/// starts before its database would otherwise fail its first connection and wait for the startup probe to restart it.
/// Registered before Marten, so it runs first (hosted services start in registration order).
/// </summary>
internal sealed partial class DatabaseReadiness(string connectionString, TimeProvider time, ILogger<DatabaseReadiness> logger) : IHostedService
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(2);
    private const int MaxAttempts = 60;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var attempt = 1;
        while (true)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                return;
            }
            catch (NpgsqlException exception) when (attempt < MaxAttempts)
            {
                LogWaiting(logger, attempt++, exception.Message);
                await Task.Delay(Delay, time, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "PostgreSQL not reachable yet (attempt {Attempt}): {Reason}")]
    private static partial void LogWaiting(ILogger logger, int attempt, string reason);
}
