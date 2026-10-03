using Ledgerline.Contracts;
using Ledgerline.Payments.Domain;
using Marten;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Ledgerline.Payments.Handlers;

/// <summary>Starts the saga once the request has been accepted and recorded (local, durable message).</summary>
public sealed record StartTransfer(Guid TransferId, string OwnerId, Guid FromAccountId, Guid ToAccountId, string ToIban, long AmountCents, string Reference);

/// <summary>Scheduled when a transfer is held for review: if no analyst decides in time, the funds are released.</summary>
public sealed record ReviewDeadlinePassed(Guid TransferId);

public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    public TimeSpan ReviewDeadline { get; set; } = TimeSpan.FromHours(48);
}

/// <summary>
/// The transfer saga (orchestration). It owns the sequence — reserve, screen, capture — and the compensations:
/// whatever goes wrong after the funds are reserved, they are released. Wolverine persists it in Marten and correlates
/// replies through their TransferId (the "{Saga}Id" convention), so contracts stay free of framework attributes.
/// Every handler checks the current status first: replies are delivered at least once and may arrive late.
/// </summary>
public sealed class Transfer : Saga
{
    public Guid Id { get; set; }

    public string OwnerId { get; set; } = string.Empty;

    public Guid FromAccountId { get; set; }

    public Guid ToAccountId { get; set; }

    public string ToIban { get; set; } = string.Empty;

    public long AmountCents { get; set; }

    public string Reference { get; set; } = string.Empty;

    public TransferStatus Status { get; set; }

    /// <summary>Distinguishes "rejected by fraud" from "failed in the ledger" once the compensation completes.</summary>
    public bool FailedInLedger { get; set; }

    public static (Transfer, ReserveFunds) Start(StartTransfer command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var saga = new Transfer
        {
            Id = command.TransferId,
            OwnerId = command.OwnerId,
            FromAccountId = command.FromAccountId,
            ToAccountId = command.ToAccountId,
            ToIban = command.ToIban,
            AmountCents = command.AmountCents,
            Reference = command.Reference,
            Status = TransferStatus.Reserving,
        };
        return (saga, new ReserveFunds(command.TransferId, command.FromAccountId, command.AmountCents, command.Reference));
    }

    public Task<OutgoingMessages> Handle(FundsReserved reply, IDocumentSession session, TimeProvider time) =>
        Status != TransferStatus.Reserving
            ? Nothing()
            : MoveToAsync(TransferStatus.Screening, null, session, time,
                new ScreenTransfer(Id, FromAccountId, ToIban, AmountCents, time.GetUtcNow()));

    public Task<OutgoingMessages> Handle(FundsReservationRejected reply, IDocumentSession session, TimeProvider time) =>
        Status != TransferStatus.Reserving ? Nothing() : MoveToAsync(TransferStatus.Failed, reply.Reason, session, time);

    public Task<OutgoingMessages> Handle(TransferCleared verdict, IDocumentSession session, TimeProvider time)
    {
        if (Status is not (TransferStatus.Screening or TransferStatus.PendingReview))
        {
            return Nothing();
        }

        var detail = string.Equals(verdict.DecidedBy, "rules", StringComparison.Ordinal) ? null : $"Approved by {verdict.DecidedBy}";
        return MoveToAsync(TransferStatus.Capturing, detail, session, time, new CaptureTransfer(Id, FromAccountId, ToAccountId, AmountCents, Reference));
    }

    public Task<OutgoingMessages> Handle(TransferHeldForReview verdict, IDocumentSession session, TimeProvider time, IOptions<PaymentsOptions> options) =>
        Status != TransferStatus.Screening
            ? Nothing()
            : MoveToAsync(TransferStatus.PendingReview, verdict.Reason, session, time,
                new ReviewDeadlinePassed(Id).DelayedFor(options.Value.ReviewDeadline));

    public Task<OutgoingMessages> Handle(TransferBlocked verdict, IDocumentSession session, TimeProvider time) =>
        Status is not (TransferStatus.Screening or TransferStatus.PendingReview)
            ? Nothing()
            : MoveToAsync(TransferStatus.Releasing, verdict.Reason, session, time, new ReleaseFunds(Id, FromAccountId, verdict.Reason));

    public Task<OutgoingMessages> Handle(ReviewDeadlinePassed timeout, IDocumentSession session, TimeProvider time) =>
        Status != TransferStatus.PendingReview
            ? Nothing()
            : MoveToAsync(TransferStatus.Releasing, "Review deadline passed", session, time, new ReleaseFunds(Id, FromAccountId, "Review deadline passed"));

    public async Task<OutgoingMessages> Handle(TransferCaptured reply, IDocumentSession session, TimeProvider time)
    {
        if (Status != TransferStatus.Capturing)
        {
            return new OutgoingMessages();
        }

        var messages = await MoveToAsync(TransferStatus.Completed, null, session, time);
        if (await NotifyBeneficiaryAsync(reply.PostedAt, session) is { } received)
        {
            messages.Add(received);
        }

        return messages;
    }

    public Task<OutgoingMessages> Handle(TransferCaptureFailed reply, IDocumentSession session, TimeProvider time)
    {
        if (Status != TransferStatus.Capturing)
        {
            return Nothing();
        }

        FailedInLedger = true;
        return MoveToAsync(TransferStatus.Releasing, reply.Reason, session, time, new ReleaseFunds(Id, FromAccountId, reply.Reason));
    }

    public Task<OutgoingMessages> Handle(FundsReleased reply, IDocumentSession session, TimeProvider time)
    {
        if (Status != TransferStatus.Releasing)
        {
            return Nothing();
        }

        var outcome = FailedInLedger ? TransferStatus.Failed : TransferStatus.Rejected;
        return MoveToAsync(outcome, null, session, time);
    }

    // Messages for a saga that already completed (late timer, duplicate reply): nothing left to do.
    public static void NotFound(ReviewDeadlinePassed timeout) => _ = timeout;

    public static void NotFound(FundsReleased reply) => _ = reply;

    public static void NotFound(TransferCaptured reply) => _ = reply;

    public static void NotFound(FundsReservationRejected reply) => _ = reply;

    private static Task<OutgoingMessages> Nothing() => Task.FromResult(new OutgoingMessages());

    /// <summary>Only another customer is told: moving money between one's own accounts already shows as the sender's transfer.</summary>
    private async Task<TransferReceived?> NotifyBeneficiaryAsync(DateTimeOffset postedAt, IDocumentSession session)
    {
        var beneficiary = await session.LoadAsync<DirectoryAccount>(ToAccountId);
        if (beneficiary?.OwnerId is not { } beneficiaryId || string.Equals(beneficiaryId, OwnerId, StringComparison.Ordinal))
        {
            return null;
        }

        var sender = await session.LoadAsync<DirectoryAccount>(FromAccountId);
        return new TransferReceived(Id, beneficiaryId, ToAccountId, sender?.Name ?? string.Empty, AmountCents, Reference, postedAt);
    }

    private async Task<OutgoingMessages> MoveToAsync(TransferStatus status, string? detail, IDocumentSession session, TimeProvider time, params object[] next)
    {
        Status = status;
        var now = time.GetUtcNow();
        if (await session.LoadAsync<TransferView>(Id) is { } view)
        {
            session.Store(view.Advance(status, now, detail));
        }

        var messages = new OutgoingMessages { new TransferStatusChanged(Id, OwnerId, status.ToString(), detail, now) };
        messages.AddRange(next);
        if (status.IsTerminal())
        {
            MarkCompleted();
        }

        return messages;
    }
}
