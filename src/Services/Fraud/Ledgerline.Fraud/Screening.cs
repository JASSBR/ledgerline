using Ledgerline.Fraud.Domain;
using Marten;

namespace Ledgerline.Fraud;

public enum ScreeningStatus
{
    Cleared,
    Blocked,
    PendingReview,
    ApprovedByAnalyst,
    RejectedByAnalyst,
}

/// <summary>The record of one screening: facts, rules hit, outcome and — for reviews — who decided and why.</summary>
public sealed record Screening(
    Guid Id,
    Guid FromAccountId,
    string ToIban,
    long AmountCents,
    DateTimeOffset RequestedAt,
    Decision Decision,
    IReadOnlyList<RuleHit> Hits,
    ScreeningStatus Status,
    string? DecidedBy = null,
    DateTimeOffset? DecidedAt = null,
    string? Comment = null)
{
    public bool IsPaid => Status is ScreeningStatus.Cleared or ScreeningStatus.ApprovedByAnalyst;
}

/// <summary>A beneficiary the bank refuses to pay (sanctions, confirmed fraud). Seeded for the demo.</summary>
public sealed record BlockedIban(string Id, string Reason);

/// <summary>Local copy of the Ledger's accounts (event-carried state): names for the review screen, no runtime call.</summary>
public sealed record KnownAccount(Guid Id, string Iban, string Name);

internal static class FraudStore
{
    public static void Configure(StoreOptions options)
    {
        options.Schema.For<Screening>().Index(screening => screening.FromAccountId).Index(screening => screening.Status);
    }
}
