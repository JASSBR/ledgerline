using Ledgerline.SharedKernel;

namespace Ledgerline.Ledger.Domain;

public static class LedgerErrors
{
    public static readonly Error AmountNotPositive =
        Error.Validation("ledger.amount_not_positive", "Amounts must be strictly positive.");

    public static readonly Error HoldNotFound =
        Error.Conflict("ledger.hold_not_found", "The referenced hold does not exist (released or already captured).");

    public static readonly Error HoldMismatch =
        Error.Conflict("ledger.hold_mismatch", "A hold with this id exists with a different amount.");

    public static readonly Error UnbalancedEntry =
        Error.Validation("ledger.unbalanced_entry", "A journal entry must have at least two lines on distinct accounts, summing to zero.");

    public static readonly Error AccountNotFound =
        Error.NotFound("ledger.account_not_found", "No account exists with this id.");

    public static Error InsufficientFunds(Money available) =>
        Error.Conflict("ledger.insufficient_funds", $"Insufficient available funds ({available}).");

    public static readonly Error FreezeReasonRequired =
        Error.Validation("ledger.freeze_reason_required", "Freezing an account requires a reason.");

    public static readonly Error AlreadyFrozen =
        Error.Conflict("ledger.already_frozen", "The account is already frozen.");

    public static readonly Error NotFrozen =
        Error.Conflict("ledger.not_frozen", "The account is not frozen.");

    public static Error AccountFrozen(string reason) =>
        Error.Conflict("ledger.account_frozen", $"The account is frozen: {reason}. Outgoing payments are blocked.");
}
