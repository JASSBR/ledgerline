using Ledgerline.Ledger.Domain;

namespace Ledgerline.Ledger.Domain.Tests;

public sealed class MoneyAndIbanTests
{
    [Fact]
    public void Money_ConvertsEurosToExactCents()
    {
        Money.FromEuros(1234.56m).Cents.ShouldBe(123_456);
        (Money.FromEuros(0.1m) + Money.FromEuros(0.2m)).ShouldBe(Money.FromEuros(0.3m));
        new Money(123_456).ToString().ShouldBe("1234.56 EUR");
    }

    [Fact]
    public void Money_RejectsSubCentPrecision()
    {
        Should.Throw<ArgumentException>(() => Money.FromEuros(1.005m));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(99_999_999_999)]
    public void Iban_GeneratedForAnAccountNumber_IsValidFrenchFormat(long accountNumber)
    {
        var iban = Iban.ForAccountNumber(accountNumber);

        iban.Value.ShouldStartWith("FR");
        iban.Value.Length.ShouldBe(27);
        Iban.TryParse(iban.Value, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(iban);
    }

    [Theory]
    [InlineData("GB82 WEST 1234 5698 7654 32")]
    [InlineData("de89370400440532013000")]
    public void Iban_AcceptsValidIbans_InAnyLayout(string input)
    {
        Iban.TryParse(input, out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData("GB82 WEST 1234 5698 7654 33")]
    [InlineData("FR76")]
    [InlineData("12345678901234567890")]
    [InlineData("")]
    [InlineData(null)]
    public void Iban_RejectsBadChecksumsAndShapes(string? input)
    {
        Iban.TryParse(input, out _).ShouldBeFalse();
    }

    [Fact]
    public void Iban_FormatsByGroupsOfFour()
    {
        Iban.ForAccountNumber(7).Formatted().Split(' ').ShouldAllBe(group => group.Length <= 4);
    }
}
