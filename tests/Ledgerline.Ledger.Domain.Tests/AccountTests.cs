using Ledgerline.Ledger.Domain;
using static Ledgerline.Ledger.Domain.Tests.Accounts;

namespace Ledgerline.Ledger.Domain.Tests;

public sealed class AccountTests
{
    private static readonly Money Fifty = Money.FromEuros(50);

    [Fact]
    public void PlaceHold_ReservesFunds_WithoutChangingTheBalance()
    {
        var account = Customer(10_000);
        var holdId = Guid.CreateVersion7();

        var held = account.Apply(account.PlaceHold(holdId, Fifty, "TRF-1", Now).Value);

        held.Balance.ShouldBe(new Money(10_000));
        held.Available.ShouldBe(new Money(5_000));
    }

    [Fact]
    public void PlaceHold_IsIdempotent_ForTheSameHold()
    {
        var holdId = Guid.CreateVersion7();
        var account = Customer(10_000);
        account = account.Apply(account.PlaceHold(holdId, Fifty, "TRF-1", Now).Value);

        account.PlaceHold(holdId, Fifty, "TRF-1", Now).Value.ShouldBeEmpty();
        account.PlaceHold(holdId, Money.FromEuros(60), "TRF-1", Now).Error.ShouldBe(LedgerErrors.HoldMismatch);
    }

    [Fact]
    public void PlaceHold_RefusesMoreThanAvailable_OnCustomerAccounts()
    {
        var account = Customer(4_999);

        account.PlaceHold(Guid.CreateVersion7(), Fifty, "TRF-1", Now).Error!.Code.ShouldBe("ledger.insufficient_funds");
    }

    [Fact]
    public void InternalAccounts_MayGoNegative()
    {
        var treasury = Treasury();

        treasury.Post(Guid.CreateVersion7(), -Fifty, "funding", Guid.CreateVersion7(), null, Now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ReleaseHold_GivesFundsBack_AndIsANoOpTheSecondTime()
    {
        var holdId = Guid.CreateVersion7();
        var account = Customer(10_000);
        account = account.Apply(account.PlaceHold(holdId, Fifty, "TRF-1", Now).Value);

        account = account.Apply(account.ReleaseHold(holdId, "fraud rejected", Now));

        account.Available.ShouldBe(new Money(10_000));
        account.ReleaseHold(holdId, "redelivered", Now).ShouldBeEmpty();
    }

    [Fact]
    public void Post_CapturingAHold_ConsumesIt_InTheSameEvent()
    {
        var holdId = Guid.CreateVersion7();
        var account = Customer(10_000);
        account = account.Apply(account.PlaceHold(holdId, Fifty, "TRF-1", Now).Value);

        account = account.Apply(account.Post(Guid.CreateVersion7(), -Fifty, "TRF-1", Guid.CreateVersion7(), holdId, Now).Value);

        account.Balance.ShouldBe(new Money(5_000));
        account.Available.ShouldBe(new Money(5_000));
        account.Holds.ShouldBeEmpty();
    }

    [Fact]
    public void Post_WithAHoldOfAnotherAmount_IsRefused()
    {
        var holdId = Guid.CreateVersion7();
        var account = Customer(10_000);
        account = account.Apply(account.PlaceHold(holdId, Fifty, "TRF-1", Now).Value);

        account.Post(Guid.CreateVersion7(), Money.FromEuros(-40), "TRF-1", Guid.CreateVersion7(), holdId, Now).Error.ShouldBe(LedgerErrors.HoldMismatch);
        account.Post(Guid.CreateVersion7(), -Fifty, "TRF-1", Guid.CreateVersion7(), Guid.CreateVersion7(), Now).Error.ShouldBe(LedgerErrors.HoldNotFound);
    }

    [Fact]
    public void Post_IsIdempotentPerEntry()
    {
        var entryId = Guid.CreateVersion7();
        var account = Customer(10_000);
        account = account.Apply(account.Post(entryId, -Fifty, "TRF-1", Guid.CreateVersion7(), null, Now).Value);

        account.Post(entryId, -Fifty, "TRF-1", Guid.CreateVersion7(), null, Now).Value.ShouldBeEmpty();
        account.Balance.ShouldBe(new Money(5_000));
    }

    [Fact]
    public void Replay_RebuildsTheSameState_AsEvolvingStepByStep()
    {
        var id = Guid.CreateVersion7();
        var holdId = Guid.CreateVersion7();
        IAccountEvent[] history =
        [
            new AccountOpened(id, Iban.ForAccountNumber(3).Value, "Alice", "alice", AccountKind.Customer, Now),
            new EntryPosted(Guid.CreateVersion7(), 20_000, "deposit", Guid.Empty, null, Now),
            new FundsHeld(holdId, 5_000, "TRF-1", Now),
            new EntryPosted(Guid.CreateVersion7(), -5_000, "TRF-1", Guid.Empty, holdId, Now),
        ];

        var account = Account.Replay(history)!;

        account.Balance.ShouldBe(new Money(15_000));
        account.Available.ShouldBe(new Money(15_000));
        Account.Replay(history[..3])!.Available.ShouldBe(new Money(15_000));
    }
}
