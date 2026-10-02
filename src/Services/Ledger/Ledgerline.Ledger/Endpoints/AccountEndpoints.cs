using System.Security.Claims;
using JasperFx.Events;
using Ledgerline.Hosting;
using Ledgerline.Ledger.Domain;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ledgerline.Ledger.Endpoints;

internal static class AccountEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var accounts = api.MapGroup("/accounts").WithTags("Accounts").RequireAuthorization();
        accounts.MapGet("/", ListAsync).WithSummary("Accounts of the caller (all accounts for operators)");
        accounts.MapGet("/{id:guid}", GetAsync).WithSummary("Balance, available balance and pending holds");
        accounts.MapGet("/{id:guid}/statement", StatementAsync).WithSummary("Posted lines, newest first, with running balance");
        accounts.MapGet("/{id:guid}/balance", BalanceAsOfAsync).WithSummary("Balance at any past value date, replayed from the event stream");
        accounts.MapGet("/lookup", LookupAsync).WithSummary("Confirms who owns an IBAN before paying it (verification of payee)");
    }

    private static async Task<Ok<List<AccountResponse>>> ListAsync(ClaimsPrincipal user, IQuerySession session, CancellationToken cancellationToken)
    {
        var query = session.Query<Account>();
        var accounts = user.IsOperator()
            ? await query.OrderBy(account => account.Name).ToListAsync(cancellationToken)
            : await query.Where(account => account.OwnerId == user.UserId()).OrderBy(account => account.Name).ToListAsync(cancellationToken);
        return TypedResults.Ok(accounts.Select(AccountResponse.From).ToList());
    }

    private static async Task<IResult> GetAsync(Guid id, ClaimsPrincipal user, IQuerySession session, CancellationToken cancellationToken)
    {
        var account = await LoadVisibleAsync(id, user, session, cancellationToken);
        return account is null ? LedgerErrors.AccountNotFound.ToProblem() : TypedResults.Ok(AccountResponse.From(account));
    }

    private static async Task<IResult> StatementAsync(Guid id, ClaimsPrincipal user, IQuerySession session, int? limit, CancellationToken cancellationToken)
    {
        var account = await LoadVisibleAsync(id, user, session, cancellationToken);
        if (account is null)
        {
            return LedgerErrors.AccountNotFound.ToProblem();
        }

        // The stream is the statement: no separate table to keep in sync. Value dates order the lines.
        var events = await session.Events.FetchStreamAsync(id, token: cancellationToken);
        var postings = events.Select(e => e.Data).OfType<EntryPosted>().OrderBy(posted => posted.PostedAt).ToList();
        var names = await NamesAsync(session, postings.Select(posted => posted.CounterpartyAccountId), cancellationToken);

        var running = 0L;
        var lines = postings.Select(posted =>
        {
            running += posted.AmountCents;
            return new StatementLineResponse(
                posted.EntryId,
                posted.PostedAt,
                posted.Reference,
                new Money(posted.AmountCents).ToEuros(),
                names.GetValueOrDefault(posted.CounterpartyAccountId, "—"),
                new Money(running).ToEuros());
        }).Reverse().Take(Math.Clamp(limit ?? 50, 1, 200)).ToList();

        return TypedResults.Ok(lines);
    }

    /// <summary>
    /// Event sourcing's party trick, for real: the account as it stood at a past value date is a replay of its stream
    /// up to that date — no history table, no audit log reconstruction.
    /// </summary>
    private static async Task<IResult> BalanceAsOfAsync(Guid id, DateTimeOffset asOf, ClaimsPrincipal user, IQuerySession session, CancellationToken cancellationToken)
    {
        if (await LoadVisibleAsync(id, user, session, cancellationToken) is null)
        {
            return LedgerErrors.AccountNotFound.ToProblem();
        }

        var events = await session.Events.FetchStreamAsync(id, token: cancellationToken);
        // Value dates filter movements, not the account's existence: money can be booked with a value date earlier
        // than the day the account was opened in this system (migrated history, back-dated corrections).
        var state = Account.Replay(events.Select(e => e.Data).OfType<IAccountEvent>().Where(e => e is AccountOpened || ValueDate(e) <= asOf));
        return TypedResults.Ok(new BalanceAsOfResponse(id, asOf, state?.Balance.ToEuros() ?? 0, state?.Available.ToEuros() ?? 0));
    }

    private static async Task<Ok<IbanLookupResponse>> LookupAsync(string iban, IQuerySession session, CancellationToken cancellationToken)
    {
        var normalized = Iban.TryParse(iban, out var parsed) ? parsed.Value : iban;
        var account = await session.Query<Account>().FirstOrDefaultAsync(a => a.Iban == normalized, cancellationToken);
        // Only the holder's name is revealed, as banks do for verification of payee — never balances or account ids.
        return TypedResults.Ok(new IbanLookupResponse(normalized, account?.Name ?? string.Empty, account is not null));
    }

    internal static async Task<Account?> LoadVisibleAsync(Guid id, ClaimsPrincipal user, IQuerySession session, CancellationToken cancellationToken)
    {
        var account = await session.LoadAsync<Account>(id, cancellationToken);
        // A customer asking for someone else's account gets the same 404 as for a missing one: no existence oracle.
        return account is not null && (user.IsOperator() || string.Equals(account.OwnerId, user.UserId(), StringComparison.Ordinal)) ? account : null;
    }

    internal static async Task<Dictionary<Guid, string>> NamesAsync(IQuerySession session, IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToArray();
        var accounts = await session.LoadManyAsync<Account>(cancellationToken, distinct);
        return accounts.ToDictionary(account => account.Id, account => account.Name);
    }

    private static DateTimeOffset ValueDate(IAccountEvent e) => e switch
    {
        FundsHeld held => held.HeldAt,
        HoldReleased released => released.ReleasedAt,
        EntryPosted posted => posted.PostedAt,
        _ => DateTimeOffset.MinValue,
    };
}
