using Ledgerline.Contracts;
using Ledgerline.Ledger.Domain;
using Marten;
using Wolverine;

namespace Ledgerline.Ledger.Handlers;

public static class RequestAccountDirectoryHandler
{
    /// <returns>One AccountRegistered per account, published to every subscriber through the outbox.</returns>
    public static async Task<OutgoingMessages> Handle(RequestAccountDirectory request, IQuerySession session, CancellationToken cancellationToken)
    {
        var accounts = await session.Query<Account>().ToListAsync(cancellationToken);
        var messages = new OutgoingMessages();
        messages.AddRange(accounts.Select(account => new AccountRegistered(account.Id, account.Iban, account.Name, account.OwnerId, account.Kind.ToString())));
        return messages;
    }
}
