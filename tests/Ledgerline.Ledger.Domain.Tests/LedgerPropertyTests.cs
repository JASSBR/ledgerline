using CsCheck;
using Ledgerline.Ledger.Domain;
using static Ledgerline.Ledger.Domain.Tests.Accounts;

namespace Ledgerline.Ledger.Domain.Tests;

/// <summary>
/// Properties that must hold for ANY sequence of operations, checked on thousands of random sequences
/// (CsCheck shrinks any failure to the smallest sequence that breaks it).
/// </summary>
public sealed class LedgerPropertyTests
{
    private interface IOperation;

    private sealed record Deposit(int To, long Cents) : IOperation;

    private sealed record Transfer(int From, int To, long Cents) : IOperation;

    private sealed record HoldThenCapture(int From, int To, long Cents) : IOperation;

    private sealed record HoldThenRelease(int From, long Cents) : IOperation;

    private static readonly Gen<IOperation> AnyOperation = Gen.OneOf<IOperation>(
        Gen.Select(Gen.Int[0, 3], Gen.Long[1, 500_000], (to, cents) => new Deposit(to, cents)),
        Gen.Select(Gen.Int[0, 3], Gen.Int[0, 3], Gen.Long[1, 500_000], (from, to, cents) => new Transfer(from, to, cents)),
        Gen.Select(Gen.Int[0, 3], Gen.Int[0, 3], Gen.Long[1, 500_000], (from, to, cents) => new HoldThenCapture(from, to, cents)),
        Gen.Select(Gen.Int[0, 3], Gen.Long[1, 500_000], (from, cents) => new HoldThenRelease(from, cents)));

    [Fact]
    public void MoneyIsNeverCreatedOrDestroyed_AndCustomersNeverGoOverdrawn()
    {
        AnyOperation.Array[1, 60].Sample(operations =>
        {
            var treasury = Treasury();
            var customers = Enumerable.Range(0, 4).Select(_ => Customer()).ToArray();
            var all = new Dictionary<Guid, Account> { [treasury.Id] = treasury };
            foreach (var customer in customers)
            {
                all[customer.Id] = customer;
            }

            foreach (var operation in operations)
            {
                Run(operation, treasury.Id, customers.Select(c => c.Id).ToArray(), all);
            }

            // Double entry: every movement has an equal and opposite line, so the books always sum to zero.
            all.Values.Sum(account => account.Balance.Cents).ShouldBe(0);
            all.Values.Where(account => account.Kind == AccountKind.Customer)
                .ShouldAllBe(account => account.Available.Cents >= 0 && account.Holds.IsEmpty);
        }, iter: 2_000);
    }

    private static void Run(IOperation operation, Guid treasury, Guid[] customers, Dictionary<Guid, Account> all)
    {
        switch (operation)
        {
            case Deposit deposit:
                Post(all, [new JournalLine(treasury, new Money(-deposit.Cents)), new JournalLine(customers[deposit.To], new Money(deposit.Cents))]);
                break;
            case Transfer transfer when transfer.From != transfer.To:
                Post(all, [new JournalLine(customers[transfer.From], new Money(-transfer.Cents)), new JournalLine(customers[transfer.To], new Money(transfer.Cents))]);
                break;
            case HoldThenCapture capture when capture.From != capture.To:
                var holdId = Guid.CreateVersion7();
                var from = all[customers[capture.From]];
                var held = from.PlaceHold(holdId, new Money(capture.Cents), "hold", Now);
                if (held.IsSuccess)
                {
                    all[from.Id] = from.Apply(held.Value);
                    Post(all, [new JournalLine(from.Id, new Money(-capture.Cents), holdId), new JournalLine(customers[capture.To], new Money(capture.Cents))]);
                }

                break;
            case HoldThenRelease release:
                var releaseHoldId = Guid.CreateVersion7();
                var account = all[customers[release.From]];
                var placed = account.PlaceHold(releaseHoldId, new Money(release.Cents), "hold", Now);
                if (placed.IsSuccess)
                {
                    account = account.Apply(placed.Value);
                    all[account.Id] = account.Apply(account.ReleaseHold(releaseHoldId, "released", Now));
                }

                break;
        }
    }

    // A refused entry (insufficient funds) changes nothing: that is part of the property being checked.
    private static void Post(Dictionary<Guid, Account> all, JournalLine[] lines)
    {
        var decided = JournalEntry.Decide(Guid.CreateVersion7(), "op", lines, all, Now);
        if (decided.IsFailure)
        {
            return;
        }

        foreach (var (accountId, events) in decided.Value)
        {
            all[accountId] = all[accountId].Apply(events);
        }
    }
}
