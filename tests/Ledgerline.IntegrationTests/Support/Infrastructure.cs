using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Ledgerline.IntegrationTests.Support;

/// <summary>One PostgreSQL and one RabbitMQ for the whole run; each service under test gets its own database.</summary>
public sealed class Infrastructure : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:4-management-alpine").Build();


    public async ValueTask InitializeAsync() => await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _rabbit.DisposeAsync();
    }

    /// <summary>
    /// A RabbitMQ virtual host per group of services under test: queues are named by service and message type,
    /// so two groups sharing a vhost would consume each other's messages.
    /// </summary>
    public async Task<string> CreateVirtualHostAsync(string name)
    {
        var vhost = $"{name}-{Guid.NewGuid():N}";
        foreach (var command in new[] { new[] { "rabbitmqctl", "add_vhost", vhost }, ["rabbitmqctl", "set_permissions", "-p", vhost, "rabbitmq", ".*", ".*", ".*"] })
        {
            var result = await _rabbit.ExecAsync(command);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(result.Stderr);
            }
        }

        return new UriBuilder(_rabbit.GetConnectionString()) { Path = "/" + vhost }.Uri.ToString();
    }

    /// <summary>A fresh database per test class keeps classes independent without paying a container per class.</summary>
    public async Task<string> CreateDatabaseAsync(string name)
    {
        // PostgreSQL identifiers are limited to 63 bytes; name + 32 hex chars stays well below.
        var database = $"{name}_{Guid.NewGuid():N}";
        var result = await _postgres.ExecScriptAsync($"CREATE DATABASE {database};");
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.Stderr);
        }

        return new Npgsql.NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = database }.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class InfrastructureGroup : ICollectionFixture<Infrastructure>
{
    public const string Name = "infrastructure";
}
