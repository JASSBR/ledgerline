namespace Ledgerline.Fraud.Domain;

public enum Decision
{
    Clear,
    Review,
    Block,
}

public sealed record TransferFacts(Guid TransferId, Guid FromAccountId, string ToIban, long AmountCents, DateTimeOffset RequestedAt);

/// <summary>What the fraud service already knows, gathered from its own records before the rules run.</summary>
public sealed record ScreeningHistory(int RecentTransfersFromAccount, bool BeneficiaryKnown, bool BeneficiaryBlocked);

public sealed record RuleHit(string Rule, Decision Outcome, string Explanation);

public sealed record Verdict(Decision Decision, IReadOnlyList<RuleHit> Hits)
{
    public string Reason => Hits.Count == 0
        ? "No rule triggered."
        : string.Join(" ", Hits.Where(hit => hit.Outcome == Decision).Select(hit => hit.Explanation));
}

/// <summary>Thresholds tuned by the risk team (configuration "Fraud:Policy"), not hard-coded in the rules.</summary>
public sealed record FraudPolicy
{
    public long SingleTransferLimitCents { get; init; } = 2_000_000;

    public long LargeAmountCents { get; init; } = 300_000;

    public long NewBeneficiaryThresholdCents { get; init; } = 100_000;

    public int VelocityLimit { get; init; } = 3;

    public TimeSpan VelocityWindow { get; init; } = TimeSpan.FromMinutes(10);
}

/// <summary>
/// Explainable rules, deliberately: an analyst must be able to tell a customer why a payment was stopped.
/// The strictest outcome wins; every triggered rule is reported, not only the decisive one.
/// </summary>
public static class FraudRules
{
    public static Verdict Evaluate(TransferFacts facts, ScreeningHistory history, FraudPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(policy);

        var hits = new List<RuleHit>();
        if (history.BeneficiaryBlocked)
        {
            hits.Add(new RuleHit("blocked-beneficiary", Decision.Block, "The beneficiary is on the bank's blocklist."));
        }

        if (facts.AmountCents > policy.SingleTransferLimitCents)
        {
            hits.Add(new RuleHit("single-transfer-limit", Decision.Block, $"Above the {policy.SingleTransferLimitCents / 100:N0} € single-transfer limit."));
        }
        else if (facts.AmountCents >= policy.LargeAmountCents)
        {
            hits.Add(new RuleHit("large-amount", Decision.Review, $"Large amount (≥ {policy.LargeAmountCents / 100:N0} €)."));
        }

        if (history.RecentTransfersFromAccount >= policy.VelocityLimit)
        {
            hits.Add(new RuleHit("velocity", Decision.Review, $"{history.RecentTransfersFromAccount} transfers from this account in the last {policy.VelocityWindow.TotalMinutes:N0} minutes."));
        }

        if (!history.BeneficiaryKnown && facts.AmountCents >= policy.NewBeneficiaryThresholdCents)
        {
            hits.Add(new RuleHit("new-beneficiary", Decision.Review, $"First payment to this beneficiary, ≥ {policy.NewBeneficiaryThresholdCents / 100:N0} €."));
        }

        var decision = hits.Count == 0 ? Decision.Clear : hits.Max(hit => hit.Outcome);
        return new Verdict(decision, hits);
    }
}
