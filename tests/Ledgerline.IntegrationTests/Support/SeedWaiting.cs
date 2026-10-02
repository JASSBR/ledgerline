using Ledgerline.Ledger;
using Ledgerline.Ledger.Domain;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ledgerline.IntegrationTests.Support;

internal static class SeedWaiting
{
    public static async Task WaitForSeedAsync(this IHost host)
    {
        var store = host.Services.GetRequiredService<IDocumentStore>();
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var session = store.QuerySession();
            var bob = await session.LoadAsync<Account>(DemoAccounts.BobCurrent);
            if (bob is not null && bob.Balance.Cents == 370_000)
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("Demo bank seed did not complete.");
    }
}
