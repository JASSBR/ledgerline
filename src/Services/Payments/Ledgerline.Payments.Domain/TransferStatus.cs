namespace Ledgerline.Payments.Domain;

/// <summary>Where a transfer stands. Terminal states: Completed, Rejected, Failed.</summary>
public enum TransferStatus
{
    /// <summary>Asking the Ledger to reserve the funds on the payer's account.</summary>
    Reserving,

    /// <summary>Funds reserved; Fraud is screening the payment.</summary>
    Screening,

    /// <summary>Held by Fraud for an analyst's decision; the funds stay reserved meanwhile.</summary>
    PendingReview,

    /// <summary>Approved; the Ledger is posting the journal entry.</summary>
    Capturing,

    /// <summary>Compensation: giving the reserved funds back after a rejection or a failed capture.</summary>
    Releasing,

    Completed,

    Rejected,

    Failed,
}

public static class TransferStatusExtensions
{
    public static bool IsTerminal(this TransferStatus status) =>
        status is TransferStatus.Completed or TransferStatus.Rejected or TransferStatus.Failed;
}
