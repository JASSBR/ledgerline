using Ledgerline.Contracts;
using Ledgerline.Ledger.Domain;
using Marten;

namespace Ledgerline.Ledger.Handlers;

/// <summary>
/// The Ledger's side of a payment. Every handler is idempotent (the transfer id is the hold id and the entry id),
/// because RabbitMQ delivery is at-least-once: a redelivered command must never move money twice.
/// </summary>
public static class TransferCommandHandler
{
    public static async Task<object> Handle(ReserveFunds command, IDocumentSession session, TimeProvider time, CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForExclusiveWriting<Account>(command.AccountId, cancellationToken);
        if (stream.Aggregate is null)
        {
            return new FundsReservationRejected(command.TransferId, LedgerErrors.AccountNotFound.Code, LedgerErrors.AccountNotFound.Description);
        }

        var result = stream.Aggregate.PlaceHold(command.TransferId, new Money(command.AmountCents), command.Reference, time.GetUtcNow());
        if (result.IsFailure)
        {
            return new FundsReservationRejected(command.TransferId, result.Error!.Code, result.Error.Description);
        }

        stream.AppendMany(result.Value);
        return new FundsReserved(command.TransferId);
    }

    public static async Task<object> Handle(CaptureTransfer command, IDocumentSession session, TimeProvider time, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var posted = await Journal.PostAsync(
            session,
            command.TransferId,
            command.Reference,
            [
                new JournalLine(command.FromAccountId, new Money(-command.AmountCents), HoldId: command.TransferId),
                new JournalLine(command.ToAccountId, new Money(command.AmountCents)),
            ],
            now,
            cancellationToken);

        return posted.IsSuccess
            ? new TransferCaptured(command.TransferId, now)
            : new TransferCaptureFailed(command.TransferId, posted.Error!.Code, posted.Error.Description);
    }

    public static async Task<FundsReleased> Handle(ReleaseFunds command, IDocumentSession session, TimeProvider time, CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForExclusiveWriting<Account>(command.AccountId, cancellationToken);
        if (stream.Aggregate is not null)
        {
            stream.AppendMany(stream.Aggregate.ReleaseHold(command.TransferId, command.Reason, time.GetUtcNow()));
        }

        return new FundsReleased(command.TransferId);
    }
}
