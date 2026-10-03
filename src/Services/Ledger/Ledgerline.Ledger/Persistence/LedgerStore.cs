using JasperFx.Events.Projections;
using Ledgerline.Ledger.Domain;
using Marten;

namespace Ledgerline.Ledger.Persistence;

internal static class LedgerStore
{
    public static void Configure(StoreOptions options)
    {
        options.Projections.Add<AccountProjection>(ProjectionLifecycle.Inline);
        options.Projections.Add<JournalProjection>(ProjectionLifecycle.Inline);
        options.Schema.For<Account>().UniqueIndex(account => account.Iban).Index(account => account.OwnerId);
        options.Events.AddEventTypes([typeof(AccountOpened), typeof(FundsHeld), typeof(HoldReleased), typeof(EntryPosted), typeof(AccountFrozen), typeof(AccountUnfrozen)]);
    }
}
