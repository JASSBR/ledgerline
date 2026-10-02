namespace Ledgerline.Contracts;

/// <summary>Every step of a transfer, for whoever wants to follow it (UI notifications, analytics).</summary>
public sealed record TransferStatusChanged(
    Guid TransferId,
    string OwnerId,
    string Status,
    string? Reason,
    DateTimeOffset OccurredAt);
