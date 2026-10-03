using Ledgerline.Ledger.Domain;

namespace Ledgerline.Ledger.Endpoints;

public sealed record HoldResponse(Guid TransferId, decimal Amount, string Reference);

public sealed record AccountResponse(
    Guid Id,
    string Iban,
    string IbanFormatted,
    string Name,
    AccountKind Kind,
    decimal Balance,
    decimal Available,
    decimal Held,
    IReadOnlyList<HoldResponse> Holds,
    bool Frozen,
    string? FrozenReason)
{
    internal static AccountResponse From(Account account) => new(
        account.Id,
        account.Iban,
        Domain.Iban.TryParse(account.Iban, out var iban) ? iban.Formatted() : account.Iban,
        account.Name,
        account.Kind,
        account.Balance.ToEuros(),
        account.Available.ToEuros(),
        account.Held.ToEuros(),
        [.. account.Holds.Values.Select(hold => new HoldResponse(hold.HoldId, hold.Amount.ToEuros(), hold.Reference))],
        account.Frozen,
        account.FrozenReason);
}

public sealed record StatementLineResponse(
    Guid EntryId,
    DateTimeOffset ValueDate,
    string Reference,
    decimal Amount,
    string Counterparty,
    decimal BalanceAfter);

public sealed record BalanceAsOfResponse(Guid AccountId, DateTimeOffset AsOf, decimal Balance, decimal Available);

public sealed record TrialBalanceLine(Guid AccountId, string Name, AccountKind Kind, decimal Balance);

/// <summary>The accountant's check: every euro on a customer account came from somewhere, so the total is zero.</summary>
public sealed record TrialBalanceResponse(IReadOnlyList<TrialBalanceLine> Accounts, decimal Total, bool Balanced, int JournalEntries);

public sealed record JournalLineResponse(Guid AccountId, string AccountName, decimal Debit, decimal Credit);

public sealed record JournalEntryResponse(Guid Id, string Reference, DateTimeOffset PostedAt, IReadOnlyList<JournalLineResponse> Lines, bool Balanced);

public sealed record DepositRequest(decimal Amount, string? Reference);

public sealed record IbanLookupResponse(string Iban, string Name, bool Exists);

public sealed record FreezeRequest(string? Reason);

/// <summary>One fact of the account's stream, as stored: the audit trail is the data itself, not a log beside it.</summary>
/// <param name="Type">opened, held, released, posted, frozen or unfrozen.</param>
public sealed record AccountEventResponse(
    long Version,
    DateTimeOffset RecordedAt,
    string Type,
    decimal? Amount,
    string? Reference,
    string? Detail);

public sealed record MovementLineResponse(
    Guid EntryId,
    Guid AccountId,
    string AccountName,
    DateTimeOffset ValueDate,
    string Reference,
    decimal Amount,
    string Counterparty);

public sealed record MonthlyFlowResponse(string Month, decimal MoneyIn, decimal MoneyOut);

/// <summary>Totals cover the whole period whatever the direction filter, so the summary never hides one side.</summary>
public sealed record MovementsResponse(
    decimal MoneyIn,
    decimal MoneyOut,
    decimal Net,
    IReadOnlyList<MonthlyFlowResponse> Months,
    IReadOnlyList<MovementLineResponse> Lines);
