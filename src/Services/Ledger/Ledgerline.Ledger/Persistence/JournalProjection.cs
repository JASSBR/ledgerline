using JasperFx.Events;
using Ledgerline.Ledger.Domain;
using Marten.Events.Projections;

namespace Ledgerline.Ledger.Persistence;

public sealed record JournalLineView(Guid AccountId, long AmountCents);

/// <summary>A journal entry as an accountant reads it: one row per account, debits and credits summing to zero.</summary>
public sealed record JournalEntryView(Guid Id, string Reference, DateTimeOffset PostedAt, IReadOnlyList<JournalLineView> Lines)
{
    public long TotalCents => Lines.Sum(line => line.AmountCents);
}

/// <summary>
/// Reassembles journal entries from the account streams: each line lives in its own account's stream,
/// grouped here by entry id. Inline, so the journal is consistent with balances at every commit.
/// </summary>
internal sealed class JournalProjection : MultiStreamProjection<JournalEntryView, Guid>
{
    public JournalProjection() => Identity<EntryPosted>(posted => posted.EntryId);

    public override JournalEntryView? Evolve(JournalEntryView? snapshot, Guid id, IEvent e) =>
        e.Data is EntryPosted posted
            ? (snapshot ?? new JournalEntryView(id, posted.Reference, posted.PostedAt, [])) with
            {
                Lines = [.. snapshot?.Lines ?? [], new JournalLineView(e.StreamId, posted.AmountCents)],
            }
            : snapshot;
}
