using Ledgerline.Ledger.Domain;

namespace Ledgerline.Ledger.Domain.Tests;

internal static class Accounts
{
    public static readonly DateTimeOffset Now = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    public static Account Customer(long balanceCents = 0) => Open(AccountKind.Customer, balanceCents);

    public static Account Treasury() => Open(AccountKind.Internal, 0);

    private static Account Open(AccountKind kind, long balanceCents)
    {
        var id = Guid.CreateVersion7();
        var account = Account.Initial(new AccountOpened(id, Iban.ForAccountNumber(1).Value, "Test", "owner", kind, Now));
        return balanceCents == 0
            ? account
            : account.Evolve(new EntryPosted(Guid.CreateVersion7(), balanceCents, "seed", Guid.Empty, null, Now));
    }

    public static Account Apply(this Account account, IEnumerable<IAccountEvent> events) =>
        events.Aggregate(account, (state, @event) => state.Evolve(@event));
}
