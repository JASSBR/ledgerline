using Ledgerline.Ledger.Domain;
using Ledgerline.Ledger.Handlers;
using Marten;
using Wolverine;

namespace Ledgerline.Ledger.Seeding;

/// <summary>A small bank with history, so the demo opens on statements that tell a story.</summary>
internal sealed partial class DemoBankSeeder(IServiceProvider services, TimeProvider time, ILogger<DemoBankSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using (var query = store.QuerySession())
        {
            if (await query.Events.FetchStreamStateAsync(DemoAccounts.Treasury, cancellationToken) is not null)
            {
                return;
            }
        }

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new OpenAccount(DemoAccounts.Treasury, "Ledgerline Treasury", null, AccountKind.Internal), cancellationToken);
        await bus.InvokeAsync(new OpenAccount(DemoAccounts.AliceCurrent, "Alice Martin — Compte courant", DemoAccounts.AliceUserId, AccountKind.Customer), cancellationToken);
        await bus.InvokeAsync(new OpenAccount(DemoAccounts.AliceSavings, "Alice Martin — Livret", DemoAccounts.AliceUserId, AccountKind.Customer), cancellationToken);
        await bus.InvokeAsync(new OpenAccount(DemoAccounts.BobCurrent, "Bob Durand — Compte courant", DemoAccounts.BobUserId, AccountKind.Customer), cancellationToken);
        await bus.InvokeAsync(new OpenAccount(DemoAccounts.ChloeCurrent, "Chloé Bernard — Compte courant", DemoAccounts.ChloeUserId, AccountKind.Customer), cancellationToken);

        // Value dates in the past: the "balance as of" view shows how each account got where it is.
        var today = time.GetUtcNow();
        (Guid Account, long Cents, string Reference, int DaysAgo)[] deposits =
        [
            (DemoAccounts.AliceCurrent, 320_000, "Salaire août", 33),
            (DemoAccounts.AliceSavings, 1_200_000, "Versement initial livret", 30),
            (DemoAccounts.BobCurrent, 185_000, "Salaire août", 32),
            (DemoAccounts.ChloeCurrent, 64_000, "Virement entrant", 20),
            (DemoAccounts.AliceCurrent, 320_000, "Salaire septembre", 3),
            (DemoAccounts.BobCurrent, 185_000, "Salaire septembre", 2),
        ];
        foreach (var (account, cents, reference, daysAgo) in deposits)
        {
            await bus.InvokeAsync(new PostDeposit(Guid.CreateVersion7(), account, cents, reference, today.AddDays(-daysAgo)), cancellationToken);
        }

        LogSeeded(logger);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo bank seeded")]
    private static partial void LogSeeded(ILogger logger);
}
