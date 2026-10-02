namespace Ledgerline.Contracts;

/// <summary>Asks Fraud to screen a transfer whose funds are already reserved.</summary>
public sealed record ScreenTransfer(
    Guid TransferId,
    Guid FromAccountId,
    string ToIban,
    long AmountCents,
    DateTimeOffset RequestedAt);

/// <summary>Screening outcomes. A transfer held for review later receives Cleared or Blocked from an analyst's decision.</summary>
public sealed record TransferCleared(Guid TransferId, IReadOnlyList<string> Rules, string DecidedBy);

public sealed record TransferBlocked(Guid TransferId, string Reason, IReadOnlyList<string> Rules, string DecidedBy);

public sealed record TransferHeldForReview(Guid TransferId, string Reason, IReadOnlyList<string> Rules);
