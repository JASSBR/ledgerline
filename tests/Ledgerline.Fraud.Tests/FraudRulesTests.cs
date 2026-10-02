using Ledgerline.Fraud.Domain;

namespace Ledgerline.Fraud.Tests;

public sealed class FraudRulesTests
{
    private static readonly FraudPolicy Policy = new();
    private static readonly ScreeningHistory Clean = new(RecentTransfersFromAccount: 0, BeneficiaryKnown: true, BeneficiaryBlocked: false);

    private static TransferFacts Facts(long cents) => new(Guid.CreateVersion7(), Guid.CreateVersion7(), "FR76…", cents, DateTimeOffset.UtcNow);

    [Fact]
    public void OrdinaryPayment_ToAKnownBeneficiary_IsCleared()
    {
        var verdict = FraudRules.Evaluate(Facts(4_500), Clean, Policy);

        verdict.Decision.ShouldBe(Decision.Clear);
        verdict.Hits.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(299_999, Decision.Clear)]
    [InlineData(300_000, Decision.Review)]
    [InlineData(2_000_000, Decision.Review)]
    [InlineData(2_000_001, Decision.Block)]
    public void AmountThresholds_AreInclusiveWhereTheyShouldBe(long cents, Decision expected)
    {
        FraudRules.Evaluate(Facts(cents), Clean, Policy).Decision.ShouldBe(expected);
    }

    [Fact]
    public void BlockedBeneficiary_IsBlocked_WhateverTheAmount()
    {
        var verdict = FraudRules.Evaluate(Facts(100), Clean with { BeneficiaryBlocked = true }, Policy);

        verdict.Decision.ShouldBe(Decision.Block);
        verdict.Reason.ShouldContain("blocklist");
    }

    [Fact]
    public void Velocity_SendsToReview()
    {
        var verdict = FraudRules.Evaluate(Facts(1_000), Clean with { RecentTransfersFromAccount = 3 }, Policy);

        verdict.Decision.ShouldBe(Decision.Review);
        verdict.Hits.ShouldHaveSingleItem().Rule.ShouldBe("velocity");
    }

    [Theory]
    [InlineData(99_999, Decision.Clear)]
    [InlineData(100_000, Decision.Review)]
    public void NewBeneficiary_IsOnlySuspiciousAboveAThreshold(long cents, Decision expected)
    {
        FraudRules.Evaluate(Facts(cents), Clean with { BeneficiaryKnown = false }, Policy).Decision.ShouldBe(expected);
    }

    [Fact]
    public void StrictestOutcomeWins_AndEveryTriggeredRuleIsReported()
    {
        var verdict = FraudRules.Evaluate(Facts(5_000_000), new ScreeningHistory(5, BeneficiaryKnown: false, BeneficiaryBlocked: false), Policy);

        verdict.Decision.ShouldBe(Decision.Block);
        verdict.Hits.Select(hit => hit.Rule).ShouldBe(["single-transfer-limit", "velocity", "new-beneficiary"]);
        verdict.Reason.ShouldContain("single-transfer limit");
        verdict.Reason.ShouldNotContain("transfers from this account");
    }

    [Fact]
    public void Thresholds_ComeFromThePolicy()
    {
        var strict = new FraudPolicy { LargeAmountCents = 10_000 };

        FraudRules.Evaluate(Facts(10_000), Clean, strict).Decision.ShouldBe(Decision.Review);
        FraudRules.Evaluate(Facts(10_000), Clean, Policy).Decision.ShouldBe(Decision.Clear);
    }
}
