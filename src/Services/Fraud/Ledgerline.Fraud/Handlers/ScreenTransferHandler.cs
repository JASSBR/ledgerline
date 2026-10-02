using Ledgerline.Contracts;
using Ledgerline.Fraud.Domain;
using Marten;
using Microsoft.Extensions.Options;

namespace Ledgerline.Fraud.Handlers;

public static class ScreenTransferHandler
{
    /// <summary>Idempotent: a redelivered request gets the recorded outcome again instead of a second screening.</summary>
    public static async Task<object> Handle(ScreenTransfer request, IDocumentSession session, IOptions<FraudPolicy> policy, CancellationToken cancellationToken)
    {
        if (await session.LoadAsync<Screening>(request.TransferId, cancellationToken) is { } existing)
        {
            return Outcome(existing);
        }

        var since = request.RequestedAt - policy.Value.VelocityWindow;
        var recent = await session.Query<Screening>()
            .CountAsync(s => s.FromAccountId == request.FromAccountId && s.RequestedAt >= since, cancellationToken);
        var known = await session.Query<Screening>()
            .AnyAsync(s => s.FromAccountId == request.FromAccountId && s.ToIban == request.ToIban
                && (s.Status == ScreeningStatus.Cleared || s.Status == ScreeningStatus.ApprovedByAnalyst), cancellationToken);
        var blocked = await session.LoadAsync<BlockedIban>(request.ToIban, cancellationToken) is not null;

        var verdict = FraudRules.Evaluate(
            new TransferFacts(request.TransferId, request.FromAccountId, request.ToIban, request.AmountCents, request.RequestedAt),
            new ScreeningHistory(recent, known, blocked),
            policy.Value);

        var screening = new Screening(
            request.TransferId,
            request.FromAccountId,
            request.ToIban,
            request.AmountCents,
            request.RequestedAt,
            verdict.Decision,
            verdict.Hits,
            verdict.Decision switch
            {
                Decision.Clear => ScreeningStatus.Cleared,
                Decision.Block => ScreeningStatus.Blocked,
                _ => ScreeningStatus.PendingReview,
            });
        session.Store(screening);
        return Outcome(screening);
    }

    internal static object Outcome(Screening screening)
    {
        var rules = screening.Hits.Select(hit => hit.Rule).ToList();
        var reason = new Verdict(screening.Decision, screening.Hits).Reason;
        return screening.Status switch
        {
            ScreeningStatus.Cleared => new TransferCleared(screening.Id, rules, "rules"),
            ScreeningStatus.ApprovedByAnalyst => new TransferCleared(screening.Id, rules, screening.DecidedBy ?? "analyst"),
            ScreeningStatus.PendingReview => new TransferHeldForReview(screening.Id, reason, rules),
            ScreeningStatus.RejectedByAnalyst => new TransferBlocked(screening.Id, screening.Comment ?? reason, rules, screening.DecidedBy ?? "analyst"),
            _ => new TransferBlocked(screening.Id, reason, rules, "rules"),
        };
    }
}

public static class AccountRegisteredHandler
{
    public static void Handle(AccountRegistered registered, IDocumentSession session) =>
        session.Store(new KnownAccount(registered.AccountId, registered.Iban, registered.Name));
}
