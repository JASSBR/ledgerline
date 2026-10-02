using Ledgerline.Fraud.Domain;
using Ledgerline.SharedKernel;
using Marten;
using Wolverine;

namespace Ledgerline.Fraud.Handlers;

/// <summary>An analyst's decision on a transfer held for review.</summary>
public sealed record DecideReview(Guid TransferId, bool Approve, string Analyst, string? Comment);

public static class FraudErrors
{
    public static readonly Error ScreeningNotFound = Error.NotFound("fraud.screening_not_found", "No screening exists for this transfer.");

    public static readonly Error AlreadyDecided = Error.Conflict("fraud.already_decided", "This transfer is not (or no longer) waiting for a review.");

    public static readonly Error CommentRequired = Error.Validation("fraud.comment_required", "A comment is required to reject a transfer.");
}

public static class DecideReviewHandler
{
    /// <returns>The decision, and the screening outcome published to Payments in the same transaction.</returns>
    public static async Task<(Result Result, OutgoingMessages Messages)> Handle(
        DecideReview command,
        IDocumentSession session,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var messages = new OutgoingMessages();
        var screening = await session.LoadAsync<Screening>(command.TransferId, cancellationToken);
        if (screening is null)
        {
            return (FraudErrors.ScreeningNotFound, messages);
        }

        if (screening.Status != ScreeningStatus.PendingReview)
        {
            return (FraudErrors.AlreadyDecided, messages);
        }

        if (!command.Approve && string.IsNullOrWhiteSpace(command.Comment))
        {
            return (FraudErrors.CommentRequired, messages);
        }

        var decided = screening with
        {
            Status = command.Approve ? ScreeningStatus.ApprovedByAnalyst : ScreeningStatus.RejectedByAnalyst,
            DecidedBy = command.Analyst,
            DecidedAt = time.GetUtcNow(),
            Comment = command.Comment?.Trim(),
        };
        session.Store(decided);
        messages.Add(ScreenTransferHandler.Outcome(decided));
        return (Result.Success(), messages);
    }
}
