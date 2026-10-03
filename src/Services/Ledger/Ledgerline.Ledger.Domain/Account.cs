using System.Collections.Immutable;
using Ledgerline.SharedKernel;

namespace Ledgerline.Ledger.Domain;

public sealed record Hold(Guid HoldId, Money Amount, string Reference);

/// <summary>
/// The state of an account, rebuilt from its events. Immutable: <see cref="Evolve"/> returns a new state,
/// so the same function serves the write side, temporal queries ("balance as of") and tests.
/// </summary>
public sealed record Account(
    Guid Id,
    string Iban,
    string Name,
    string? OwnerId,
    AccountKind Kind,
    Money Balance,
    ImmutableDictionary<Guid, Hold> Holds,
    ImmutableHashSet<Guid> PostedEntries,
    string? FrozenReason = null)
{
    public bool Frozen => FrozenReason is not null;

    public Money Held => new(Holds.Values.Sum(hold => hold.Amount.Cents));

    public Money Available => Balance - Held;

    public static Account Initial(AccountOpened opened)
    {
        ArgumentNullException.ThrowIfNull(opened);
        return new Account(opened.AccountId, opened.Iban, opened.Name, opened.OwnerId, opened.Kind, Money.Zero, ImmutableDictionary<Guid, Hold>.Empty, []);
    }

    public Account Evolve(IAccountEvent @event) => @event switch
    {
        FundsHeld held => this with { Holds = Holds.SetItem(held.HoldId, new Hold(held.HoldId, new Money(held.AmountCents), held.Reference)) },
        HoldReleased released => this with { Holds = Holds.Remove(released.HoldId) },
        EntryPosted posted => this with
        {
            Balance = Balance + new Money(posted.AmountCents),
            Holds = posted.HoldId is { } holdId ? Holds.Remove(holdId) : Holds,
            PostedEntries = PostedEntries.Add(posted.EntryId),
        },
        AccountFrozen frozen => this with { FrozenReason = frozen.Reason },
        AccountUnfrozen => this with { FrozenReason = null },
        _ => this,
    };

    public static Account? Replay(IEnumerable<IAccountEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        Account? state = null;
        foreach (var @event in events)
        {
            state = @event is AccountOpened opened ? Initial(opened) : state?.Evolve(@event);
        }

        return state;
    }

    /// <summary>
    /// Reserve funds for a payment. Idempotent: the same hold placed twice (a redelivered message) yields no new event.
    /// </summary>
    public Result<IReadOnlyList<IAccountEvent>> PlaceHold(Guid holdId, Money amount, string reference, DateTimeOffset now)
    {
        if (Holds.TryGetValue(holdId, out var existing))
        {
            return existing.Amount == amount
                ? Result.Success<IReadOnlyList<IAccountEvent>>([])
                : LedgerErrors.HoldMismatch;
        }

        if (!amount.IsPositive)
        {
            return LedgerErrors.AmountNotPositive;
        }

        if (Frozen)
        {
            return LedgerErrors.AccountFrozen(FrozenReason!);
        }

        if (Kind == AccountKind.Customer && Available < amount)
        {
            return LedgerErrors.InsufficientFunds(Available);
        }

        return Result.Success<IReadOnlyList<IAccountEvent>>([new FundsHeld(holdId, amount.Cents, reference, now)]);
    }

    /// <summary>Gives reserved funds back. Releasing an unknown or already-consumed hold is a no-op (idempotent compensation).</summary>
    public IReadOnlyList<IAccountEvent> ReleaseHold(Guid holdId, string reason, DateTimeOffset now) =>
        Holds.ContainsKey(holdId) ? [new HoldReleased(holdId, reason, now)] : [];

    /// <summary>
    /// Records this account's line of a journal entry. A debit either consumes a hold of exactly that amount or must be
    /// covered by the available balance (customer accounts). Idempotent per entry id.
    /// </summary>
    public Result<IReadOnlyList<IAccountEvent>> Post(Guid entryId, Money signedAmount, string reference, Guid counterparty, Guid? holdId, DateTimeOffset now)
    {
        if (PostedEntries.Contains(entryId))
        {
            return Result.Success<IReadOnlyList<IAccountEvent>>([]);
        }

        if (signedAmount == Money.Zero)
        {
            return LedgerErrors.AmountNotPositive;
        }

        if (holdId is { } id)
        {
            if (!Holds.TryGetValue(id, out var hold))
            {
                return LedgerErrors.HoldNotFound;
            }

            if (hold.Amount != -signedAmount)
            {
                return LedgerErrors.HoldMismatch;
            }
        }
        else if (signedAmount < Money.Zero && Frozen)
        {
            return LedgerErrors.AccountFrozen(FrozenReason!);
        }
        else if (signedAmount < Money.Zero && Kind == AccountKind.Customer && Available < -signedAmount)
        {
            return LedgerErrors.InsufficientFunds(Available);
        }

        return Result.Success<IReadOnlyList<IAccountEvent>>([new EntryPosted(entryId, signedAmount.Cents, reference, counterparty, holdId, now)]);
    }

    /// <summary>
    /// Blocks new debits. Holds placed before the freeze can still be captured: that money was already committed
    /// to a payment in flight, and the saga must be able to finish it.
    /// </summary>
    public Result<IReadOnlyList<IAccountEvent>> Freeze(string reason, string by, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return LedgerErrors.FreezeReasonRequired;
        }

        return Frozen
            ? LedgerErrors.AlreadyFrozen
            : Result.Success<IReadOnlyList<IAccountEvent>>([new AccountFrozen(reason.Trim(), by, now)]);
    }

    public Result<IReadOnlyList<IAccountEvent>> Unfreeze(string by, DateTimeOffset now) =>
        Frozen
            ? Result.Success<IReadOnlyList<IAccountEvent>>([new AccountUnfrozen(by, now)])
            : LedgerErrors.NotFrozen;
}
