using Ledgerline.Contracts;
using Ledgerline.Ledger.Domain;
using Marten;

namespace Ledgerline.Ledger.Handlers;

/// <summary>Opens an account. Local command: used by the seed and by operators.</summary>
public sealed record OpenAccount(Guid AccountId, string Name, string? OwnerId, AccountKind Kind);

public static class OpenAccountHandler
{
    /// <returns>The integration event, published through the outbox in the same transaction as the stream creation.</returns>
    public static async Task<AccountRegistered?> Handle(OpenAccount command, IDocumentSession session, TimeProvider time, CancellationToken cancellationToken)
    {
        if (await session.Events.FetchStreamStateAsync(command.AccountId, cancellationToken) is not null)
        {
            return null;
        }

        var iban = Iban.ForAccountNumber(Random.Shared.NextInt64(1_000_000_000, 99_999_999_999)).Value;
        session.Events.StartStream<Account>(command.AccountId, new AccountOpened(command.AccountId, iban, command.Name, command.OwnerId, command.Kind, time.GetUtcNow()));
        return new AccountRegistered(command.AccountId, iban, command.Name, command.OwnerId, command.Kind.ToString());
    }
}

/// <summary>Money entering the bank: treasury → customer. Local command used by operators and the demo seed.</summary>
public sealed record PostDeposit(Guid EntryId, Guid AccountId, long AmountCents, string Reference, DateTimeOffset ValueDate);

public static class PostDepositHandler
{
    public static async Task Handle(PostDeposit command, IDocumentSession session, CancellationToken cancellationToken) =>
        await Journal.PostAsync(
            session,
            command.EntryId,
            command.Reference,
            [new JournalLine(DemoAccounts.Treasury, new Money(-command.AmountCents)), new JournalLine(command.AccountId, new Money(command.AmountCents))],
            command.ValueDate,
            cancellationToken);
}
