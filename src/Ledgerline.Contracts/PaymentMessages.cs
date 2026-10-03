namespace Ledgerline.Contracts;

/// <summary>Every step of a transfer, for whoever wants to follow it (UI notifications, analytics).</summary>
public sealed record TransferStatusChanged(
    Guid TransferId,
    string OwnerId,
    string Status,
    string? Reason,
    DateTimeOffset OccurredAt);

/// <summary>Money landed on someone else's account: tells the beneficiary, who did not start the transfer.</summary>
public sealed record TransferReceived(
    Guid TransferId,
    string BeneficiaryId,
    Guid ToAccountId,
    string FromName,
    long AmountCents,
    string Reference,
    DateTimeOffset ReceivedAt);
