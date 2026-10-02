using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Ledgerline.IntegrationTests.Support;

/// <summary>One PostgreSQL and one RabbitMQ for the whole run; each service under test gets its own database.</summary>
public sealed class Infrastructure : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:4-management-alpine").Build();

    public string RabbitConnectionString => _rabbit.GetConnectionString();

    public async ValueTask InitializeAsync() => await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _rabbit.DisposeAsync();
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
