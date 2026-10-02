namespace Ledgerline.Ledger.Domain;

public enum AccountKind
{
    /// <summary>A customer's current account: its available balance can never go below zero.</summary>
    Customer,

    /// <summary>A bank-side account (treasury, fees, suspense): may carry a negative balance by design.</summary>
    Internal,
}

/// <summary>Facts recorded in an account's stream. Append-only: history is never rewritten, only compensated.</summary>
public interface IAccountEvent;

public sealed record AccountOpened(
    Guid AccountId,
    string Iban,
    string Name,
    string? OwnerId,
    AccountKind Kind,
    DateTimeOffset OpenedAt) : IAccountEvent;

/// <summary>Funds reserved for a pending payment: they stay in the balance but leave the available balance.</summary>
public sealed record FundsHeld(Guid HoldId, long AmountCents, string Reference, DateTimeOffset HeldAt) : IAccountEvent;

public sealed record HoldReleased(Guid HoldId, string Reason, DateTimeOffset ReleasedAt) : IAccountEvent;

/// <summary>
/// One line of a double-entry journal entry. Negative = debit (money leaves), positive = credit.
/// When it consumes a hold, the hold disappears in the same event: no window where funds count twice.
/// </summary>
public sealed record EntryPosted(
    Guid EntryId,
    long AmountCents,
    string Reference,
    Guid CounterpartyAccountId,
    Guid? HoldId,
    DateTimeOffset PostedAt) : IAccountEvent;
