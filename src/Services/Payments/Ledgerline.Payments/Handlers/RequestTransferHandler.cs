using Ledgerline.Payments.Domain;
using Ledgerline.SharedKernel;
using Marten;
using Wolverine;

namespace Ledgerline.Payments.Handlers;

public sealed record RequestTransfer(string OwnerId, string IdempotencyKey, TransferRequest Request);

public sealed record TransferAccepted(TransferView Transfer, bool Replayed);

public static class RequestTransferHandler
{
    /// <summary>
    /// Validates and records a transfer request, then starts the saga — the idempotency record, the view and the saga's
    /// start message commit together, so a crash can neither lose an accepted transfer nor start one twice.
    /// </summary>
    public static async Task<(Result<TransferAccepted> Result, OutgoingMessages Messages)> Handle(
        RequestTransfer command,
        IDocumentSession session,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var none = new OutgoingMessages();
        var valid = TransferRequestRules.Validate(command.Request, command.IdempotencyKey);
        if (valid.IsFailure)
        {
            return (valid.Error!, none);
        }

        var fingerprint = TransferRequestRules.Fingerprint(command.Request);
        var recordId = $"{command.OwnerId}:{command.IdempotencyKey}";
        if (await session.LoadAsync<IdempotencyRecord>(recordId, cancellationToken) is { } previous)
        {
            return string.Equals(previous.Fingerprint, fingerprint, StringComparison.Ordinal) && await session.LoadAsync<TransferView>(previous.TransferId, cancellationToken) is { } original
                ? (new TransferAccepted(original, Replayed: true), none)
                : (PaymentErrors.IdempotencyKeyReused, none);
        }

        var from = await session.LoadAsync<DirectoryAccount>(command.Request.FromAccountId, cancellationToken);
        if (from is null || !string.Equals(from.OwnerId, command.OwnerId, StringComparison.Ordinal))
        {
            return (PaymentErrors.SourceAccountNotFound, none);
        }

        var iban = command.Request.ToIban.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        var to = await session.Query<DirectoryAccount>().FirstOrDefaultAsync(account => account.Iban == iban, cancellationToken);
        if (to is null)
        {
            return (PaymentErrors.BeneficiaryUnknown, none);
        }

        if (to.Id == from.Id)
        {
            return (PaymentErrors.SameAccount, none);
        }

        var now = time.GetUtcNow();
        var transferId = Guid.CreateVersion7();
        var cents = (long)(command.Request.Amount * 100);
        var reference = string.IsNullOrWhiteSpace(command.Request.Label) ? $"Virement à {to.Name}" : command.Request.Label.Trim();

        // Insert, not Store: two concurrent requests with the same key collide here, and the retry replays the winner.
        session.Insert(new IdempotencyRecord(recordId, fingerprint, transferId, now));
        var view = new TransferView(transferId, command.OwnerId, from.Id, from.Name, to.Iban, to.Name, cents, reference,
            TransferStatus.Reserving, null, now, [new TransferStep(TransferStatus.Reserving, now, null)]);
        session.Store(view);

        return (new TransferAccepted(view, Replayed: false),
            [new StartTransfer(transferId, command.OwnerId, from.Id, to.Id, to.Iban, cents, reference)]);
    }
}
