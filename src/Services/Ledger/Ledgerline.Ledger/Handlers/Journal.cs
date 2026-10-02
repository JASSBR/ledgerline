using JasperFx.Events;
using Ledgerline.Ledger.Domain;
using Ledgerline.SharedKernel;
using Marten;

namespace Ledgerline.Ledger.Handlers;

internal static class Journal
{
    /// <summary>
    /// Decides a journal entry against the current state of every account involved and appends one event per line.
    /// All streams are written by one SaveChanges: the entry is atomic. FetchForWriting pins each stream's version,
    /// so a concurrent writer makes the commit fail and Wolverine retries on fresh state (no lost update, no overdraft).
    /// </summary>
    public static async Task<Result> PostAsync(
        IDocumentSession session,
        Guid entryId,
        string reference,
        IReadOnlyList<JournalLine> lines,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var streams = new Dictionary<Guid, IEventStream<Account>>();
        foreach (var accountId in lines.Select(line => line.AccountId))
        {
            streams[accountId] = await session.Events.FetchForWriting<Account>(accountId, cancellationToken);
        }

        var accounts = new Dictionary<Guid, Account>();
        foreach (var (accountId, stream) in streams)
        {
            if (stream.Aggregate is { } account)
            {
                accounts[accountId] = account;
            }
        }

        var decided = JournalEntry.Decide(entryId, reference, lines, accounts, at);
        if (decided.IsFailure)
        {
            return Result.Failure(decided.Error!);
        }

        foreach (var (accountId, events) in decided.Value)
        {
            streams[accountId].AppendMany(events);
        }

        return Result.Success();
    }
}
