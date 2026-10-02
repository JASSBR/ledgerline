namespace Ledgerline.Contracts;

// ---- Events published by the Ledger (event-carried state transfer: subscribers keep their own copy) ----

/// <summary>An account exists. Payments and Fraud keep a local directory from these, so they never query the Ledger.</summary>
public sealed record AccountRegistered(Guid AccountId, string Iban, string Name, string? OwnerId, string Kind);

// ---- Commands handled by the Ledger. The transfer id doubles as hold id and entry id: replays are idempotent. ----

public sealed record ReserveFunds(Guid TransferId, Guid AccountId, long AmountCents, string Reference);

public sealed record CaptureTransfer(Guid TransferId, Guid FromAccountId, Guid ToAccountId, long AmountCents, string Reference);

public sealed record ReleaseFunds(Guid TransferId, Guid AccountId, string Reason);

// ---- Replies from the Ledger ----

public sealed record FundsReserved(Guid TransferId);

public sealed record FundsReservationRejected(Guid TransferId, string Code, string Reason);

public sealed record TransferCaptured(Guid TransferId, DateTimeOffset PostedAt);

public sealed record TransferCaptureFailed(Guid TransferId, string Code, string Reason);

public sealed record FundsReleased(Guid TransferId);
