using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ledgerline.Fraud;
using Ledgerline.IntegrationTests.Support;
using Ledgerline.Ledger;
using Ledgerline.Ledger.Endpoints;
using Ledgerline.Payments;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ledgerline.IntegrationTests.Saga;

/// <summary>
/// The whole bank in one test process: the three real services, each on its own database, talking through one
/// RabbitMQ virtual host — the same topology as production, minus Keycloak (test authentication) and the gateway.
/// </summary>
public sealed class BankFixture : IAsyncLifetime
{
    private readonly Infrastructure _infrastructure = new();

    public ServiceFactory<LedgerService> Ledger { get; private set; } = null!;

    public ServiceFactory<PaymentsService> Payments { get; private set; } = null!;

    /// <summary>A second Payments instance on the same database and broker, as on Kubernetes (two replicas).</summary>
    public ServiceFactory<PaymentsService> PaymentsReplica { get; private set; } = null!;

    public ServiceFactory<FraudService> Fraud { get; private set; } = null!;

    public IReadOnlyDictionary<Guid, string> Ibans { get; private set; } = new Dictionary<Guid, string>();

    public static readonly TimeSpan ReviewDeadline = TimeSpan.FromSeconds(8);

    public async ValueTask InitializeAsync()
    {
        await _infrastructure.InitializeAsync();
        var vhost = await _infrastructure.CreateVirtualHostAsync("bank");

        Ledger = new ServiceFactory<LedgerService>("ledgerdb", await _infrastructure.CreateDatabaseAsync("ledger"), vhost, seed: true);
        var paymentsDatabase = await _infrastructure.CreateDatabaseAsync("payments");
        var paymentsSettings = new Dictionary<string, string>(StringComparer.Ordinal) { ["Payments:ReviewDeadline"] = ReviewDeadline.ToString() };
        Payments = new ServiceFactory<PaymentsService>("paymentsdb", paymentsDatabase, vhost, seed: false, paymentsSettings);
        PaymentsReplica = new ServiceFactory<PaymentsService>("paymentsdb", paymentsDatabase, vhost, seed: false, paymentsSettings);
        // Velocity would turn the concurrency test into a review queue; it is covered by the rules' unit tests.
        Fraud = new ServiceFactory<FraudService>("frauddb", await _infrastructure.CreateDatabaseAsync("fraud"), vhost, seed: false,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Fraud:Policy:VelocityLimit"] = "1000" });

        // Worst case on purpose: the Ledger seeds and publishes before any subscriber queue exists, so those events
        // are dropped by the broker. The subscribers' directory request at startup must still fill their copies.
        _ = Ledger.Server;
        await Ledger.Services.GetRequiredService<IHost>().WaitForSeedAsync();
        _ = Payments.Server;
        _ = PaymentsReplica.Server;
        _ = Fraud.Server;
        await WaitForDirectoryAsync(Payments.Services.GetRequiredService<IDocumentStore>(), store => store.Query<DirectoryAccount>().CountAsync());
        await WaitForDirectoryAsync(Fraud.Services.GetRequiredService<IDocumentStore>(), store => store.Query<KnownAccount>().CountAsync());

        using var ops = Client(Ledger, TestUsers.Operator);
        var accounts = await ops.GetFromJsonAsync<List<AccountResponse>>("/api/ledger/accounts", LedgerJson.Options);
        Ibans = accounts!.ToDictionary(account => account.Id, account => account.Iban);
    }

    public async ValueTask DisposeAsync()
    {
        await Ledger.DisposeAsync();
        await Payments.DisposeAsync();
        await PaymentsReplica.DisposeAsync();
        await Fraud.DisposeAsync();
        await _infrastructure.DisposeAsync();
    }

    public static HttpClient Client<T>(ServiceFactory<T> service, AuthenticationHeaderValue auth)
        where T : class
    {
        var client = service.CreateClient();
        client.DefaultRequestHeaders.Authorization = auth;
        return client;
    }

    private static async Task WaitForDirectoryAsync(IDocumentStore store, Func<IQuerySession, Task<int>> count)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var session = store.QuerySession();
            if (await count(session) >= 6)
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("Account directory was not replicated.");
    }
}

[CollectionDefinition(Name)]
public sealed class BankGroup : ICollectionFixture<BankFixture>
{
    public const string Name = "bank";
}

internal static class LedgerJson
{
    public static System.Text.Json.JsonSerializerOptions Options => Ledger.LedgerApiTests.Json;
}
