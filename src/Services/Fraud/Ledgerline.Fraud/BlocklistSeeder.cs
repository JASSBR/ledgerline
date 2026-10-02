using Marten;

namespace Ledgerline.Fraud;

/// <summary>Demo blocklist: one syntactically valid IBAN that the rules must refuse to pay.</summary>
internal sealed class BlocklistSeeder(IDocumentStore store) : IHostedService
{
    public const string DemoBlockedIban = "FR7699999000016666666666610";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var session = store.LightweightSession();
        session.Store(new BlockedIban(DemoBlockedIban, "Demo: confirmed fraud beneficiary"));
        await session.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
