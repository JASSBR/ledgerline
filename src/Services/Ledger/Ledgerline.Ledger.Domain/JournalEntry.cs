using Ledgerline.SharedKernel;

namespace Ledgerline.Ledger.Domain;

public sealed record JournalLine(Guid AccountId, Money Amount, Guid? HoldId = null);

/// <summary>
/// Double-entry bookkeeping: every movement is a journal entry whose lines sum to zero, so money is never created or
/// destroyed — it only moves between accounts. The entry is decided against every account involved, and either all
/// lines are recorded or none is.
/// </summary>
public static class JournalEntry
{
    public static Result<IReadOnlyDictionary<Guid, IReadOnlyList<IAccountEvent>>> Decide(
        Guid entryId,
        string reference,
        IReadOnlyList<JournalLine> lines,
        IReadOnlyDictionary<Guid, Account> accounts,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(accounts);

        if (lines.Count < 2
            || lines.Select(line => line.AccountId).Distinct().Count() != lines.Count
            || lines.Sum(line => line.Amount.Cents) != 0
            || lines.Any(line => line.Amount == Money.Zero))
        {
            return LedgerErrors.UnbalancedEntry;
        }

        var events = new Dictionary<Guid, IReadOnlyList<IAccountEvent>>();
        foreach (var line in lines)
        {
            if (!accounts.TryGetValue(line.AccountId, out var account))
            {
                return LedgerErrors.AccountNotFound;
            }

            // The counterparty of a two-line entry is the other account; for wider entries, the largest opposite line.
            var counterparty = lines.Where(other => other.AccountId != line.AccountId && Math.Sign(other.Amount.Cents) != Math.Sign(line.Amount.Cents))
                .OrderByDescending(other => Math.Abs(other.Amount.Cents))
                .First().AccountId;

            var decided = account.Post(entryId, line.Amount, reference, counterparty, line.HoldId, now);
            if (decided.IsFailure)
            {
                return Result.Failure<IReadOnlyDictionary<Guid, IReadOnlyList<IAccountEvent>>>(decided.Error!);
            }

            events[line.AccountId] = decided.Value;
        }

        return events;
    }
}
