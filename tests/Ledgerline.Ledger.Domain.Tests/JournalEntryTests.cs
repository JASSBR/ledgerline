using Ledgerline.Ledger.Domain;
using static Ledgerline.Ledger.Domain.Tests.Accounts;

namespace Ledgerline.Ledger.Domain.Tests;

public sealed class JournalEntryTests
{
    [Fact]
    public void Transfer_DebitsOneAccount_AndCreditsTheOther_ForTheSameEntry()
    {
        var alice = Customer(10_000);
        var bob = Customer();
        var entryId = Guid.CreateVersion7();

        var events = JournalEntry.Decide(entryId, "TRF-1",
            [new JournalLine(alice.Id, new Money(-2_500)), new JournalLine(bob.Id, new Money(2_500))],
            Map(alice, bob), Now).Value;

        var debit = events[alice.Id].ShouldHaveSingleItem().ShouldBeOfType<EntryPosted>();
        var credit = events[bob.Id].ShouldHaveSingleItem().ShouldBeOfType<EntryPosted>();
        debit.AmountCents.ShouldBe(-2_500);
        credit.AmountCents.ShouldBe(2_500);
        debit.CounterpartyAccountId.ShouldBe(bob.Id);
        credit.CounterpartyAccountId.ShouldBe(alice.Id);
        debit.EntryId.ShouldBe(credit.EntryId);
    }

    [Theory]
    [MemberData(nameof(UnbalancedEntries))]
    public void UnbalancedOrDegenerateEntries_AreRefused(long[] amounts, bool sameAccount)
    {
        var a = Customer(100_000);
        var b = Customer(100_000);
        var lines = amounts.Select((amount, index) => new JournalLine(sameAccount || index % 2 == 0 ? a.Id : b.Id, new Money(amount))).ToList();

        JournalEntry.Decide(Guid.CreateVersion7(), "x", lines, Map(a, b), Now).Error.ShouldBe(LedgerErrors.UnbalancedEntry);
    }

    public static TheoryData<long[], bool> UnbalancedEntries => new()
    {
        { [-100, 99], false },
        { [-100], false },
        { [-100, 100], true },
        { [0, 0], false },
    };

    [Fact]
    public void AFailingLine_FailsTheWholeEntry()
    {
        var poor = Customer(1_000);
        var rich = Customer(100_000);

        var result = JournalEntry.Decide(Guid.CreateVersion7(), "x",
            [new JournalLine(poor.Id, new Money(-5_000)), new JournalLine(rich.Id, new Money(5_000))],
            Map(poor, rich), Now);

        result.Error!.Code.ShouldBe("ledger.insufficient_funds");
    }

    [Fact]
    public void UnknownAccount_FailsTheEntry()
    {
        var alice = Customer(10_000);

        JournalEntry.Decide(Guid.CreateVersion7(), "x",
            [new JournalLine(alice.Id, new Money(-100)), new JournalLine(Guid.CreateVersion7(), new Money(100))],
            Map(alice), Now).Error.ShouldBe(LedgerErrors.AccountNotFound);
    }

    private static Dictionary<Guid, Account> Map(params Account[] accounts) => accounts.ToDictionary(account => account.Id);
}
