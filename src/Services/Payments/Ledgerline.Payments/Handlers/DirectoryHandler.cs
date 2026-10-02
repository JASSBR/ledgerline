using Ledgerline.Contracts;
using Marten;

namespace Ledgerline.Payments.Handlers;

public static class AccountRegisteredHandler
{
    public static void Handle(AccountRegistered registered, IDocumentSession session) =>
        session.Store(new DirectoryAccount(registered.AccountId, registered.Iban, registered.Name, registered.OwnerId, registered.Kind));
}
