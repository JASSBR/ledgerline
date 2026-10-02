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
    IReadOnlyList<HoldResponse> Holds)
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
        [.. account.Holds.Values.Select(hold => new HoldResponse(hold.HoldId, hold.Amount.ToEuros(), hold.Reference))]);
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
