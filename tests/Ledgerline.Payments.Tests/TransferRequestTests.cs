using Ledgerline.Payments.Domain;

namespace Ledgerline.Payments.Tests;

public sealed class TransferRequestTests
{
    private static readonly TransferRequest Valid = new(Guid.CreateVersion7(), "FR76 9999 9000 0100 0000 0000 123", 42.5m, "Loyer");

    [Fact]
    public void ValidRequest_Passes() => TransferRequestRules.Validate(Valid, "key-1").IsSuccess.ShouldBeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.001)]
    [InlineData(1_000_000)]
    public void InvalidAmounts_AreRefused(decimal amount) =>
        TransferRequestRules.Validate(Valid with { Amount = amount }, "key-1").Error.ShouldBe(PaymentErrors.AmountInvalid);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingIdempotencyKey_IsRefused(string? key) =>
        TransferRequestRules.Validate(Valid, key).Error.ShouldBe(PaymentErrors.IdempotencyKeyMissing);

    [Fact]
    public void OverlongKeyAndLabel_AreRefused()
    {
        TransferRequestRules.Validate(Valid, new string('k', 65)).Error.ShouldBe(PaymentErrors.IdempotencyKeyMissing);
        TransferRequestRules.Validate(Valid with { Label = new string('x', 141) }, "key").Error.ShouldBe(PaymentErrors.LabelTooLong);
    }

    [Fact]
    public void Fingerprint_IgnoresIbanLayout_ButNotTheAmountOrLabel()
    {
        var compact = Valid with { ToIban = "fr7699999000010000000000123" };

        TransferRequestRules.Fingerprint(compact).ShouldBe(TransferRequestRules.Fingerprint(Valid));
        TransferRequestRules.Fingerprint(Valid with { Amount = 42.51m }).ShouldNotBe(TransferRequestRules.Fingerprint(Valid));
        TransferRequestRules.Fingerprint(Valid with { Label = "Autre" }).ShouldNotBe(TransferRequestRules.Fingerprint(Valid));
    }
}
