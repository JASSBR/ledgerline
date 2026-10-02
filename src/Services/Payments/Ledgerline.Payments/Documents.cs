using Ledgerline.Payments.Domain;
using Marten;

namespace Ledgerline.Payments;

public sealed record TransferStep(TransferStatus Status, DateTimeOffset At, string? Detail);

/// <summary>
/// The customer-facing record of a transfer. The saga document is deleted when the saga completes;
/// this view stays, with the full timeline.
/// </summary>
public sealed record TransferView(
    Guid Id,
    string OwnerId,
    Guid FromAccountId,
    string FromName,
    string ToIban,
    string ToName,
    long AmountCents,
    string Label,
    TransferStatus Status,
    string? Reason,
    DateTimeOffset RequestedAt,
    IReadOnlyList<TransferStep> Steps)
{
    public TransferView Advance(TransferStatus status, DateTimeOffset at, string? detail) =>
        this with { Status = status, Reason = detail ?? Reason, Steps = [.. Steps, new TransferStep(status, at, detail)] };
}

/// <summary>Key = "{user}:{Idempotency-Key}": keys are scoped per user, so two customers can never collide.</summary>
public sealed record IdempotencyRecord(string Id, string Fingerprint, Guid TransferId, DateTimeOffset CreatedAt);

/// <summary>Local copy of the Ledger's accounts, kept from AccountRegistered events.</summary>
public sealed record DirectoryAccount(Guid Id, string Iban, string Name, string? OwnerId, string Kind);

internal static class PaymentsStore
{
    public static void Configure(StoreOptions options)
    {
        options.Schema.For<TransferView>().Index(view => view.OwnerId).Index(view => view.RequestedAt);
        options.Schema.For<DirectoryAccount>().UniqueIndex(account => account.Iban);
    }
}
