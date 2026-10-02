using JasperFx.Events;
using Ledgerline.Ledger.Domain;
using Marten.Events.Aggregation;

namespace Ledgerline.Ledger.Persistence;

/// <summary>
/// Keeps an up-to-date snapshot of each account, written in the same transaction as its events (inline).
/// It delegates to the domain's own <see cref="Account.Evolve"/>: one definition of "how an account changes".
/// </summary>
internal sealed class AccountProjection : SingleStreamProjection<Account, Guid>
{
    public override Account? Evolve(Account? snapshot, Guid id, IEvent e) => e.Data switch
    {
        AccountOpened opened => Account.Initial(opened),
        IAccountEvent @event when snapshot is not null => snapshot.Evolve(@event),
        _ => snapshot,
    };
}
