using JasperFx.Events;
using Ledgerline.Ledger.Domain;
using Ledgerline.SharedKernel;
using Marten;

namespace Ledgerline.Ledger.Handlers;

internal static class Journal
{
    /// <summary>
    /// Decides a journal entry against the current state of every account involved and appends one event per line.
    /// All streams are written by one SaveChanges: the entry is atomic.
    /// Each stream is locked (SELECT … FOR UPDATE) before its state is read, so concurrent postings on one account
    /// queue up instead of failing on a version conflict and retrying: a busy account (a merchant, the treasury) is the
    /// norm in banking, and under load optimistic retries ran out (ADR 0003). Locks are always taken in account-id
    /// order: Alice→Bob and Bob→Alice at the same instant would otherwise deadlock.
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
        foreach (var accountId in lines.Select(line => line.AccountId).Distinct().Order())
        {
            streams[accountId] = await session.Events.FetchForExclusiveWriting<Account>(accountId, cancellationToken);
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

        foreach (var (accountId, events) in decided.Value.OrderBy(pair => pair.Key))
        {
            streams[accountId].AppendMany(events);
        }

        return Result.Success();
    }
}
