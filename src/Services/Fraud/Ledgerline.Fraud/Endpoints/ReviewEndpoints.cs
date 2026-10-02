using System.Security.Claims;
using Ledgerline.Fraud.Domain;
using Ledgerline.Fraud.Handlers;
using Ledgerline.Hosting;
using Ledgerline.SharedKernel;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine;

namespace Ledgerline.Fraud.Endpoints;

public sealed record ScreeningResponse(
    Guid TransferId,
    string FromAccount,
    string ToIban,
    string? ToName,
    decimal Amount,
    DateTimeOffset RequestedAt,
    Decision Decision,
    ScreeningStatus Status,
    IReadOnlyList<RuleHit> Hits,
    string? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? Comment);

public sealed record ReviewDecisionRequest(bool Approve, string? Comment);

internal static class ReviewEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var reviews = api.MapGroup(string.Empty).WithTags("Fraud").RequireAuthorization(LedgerlineServiceExtensions.OperatorPolicy);
        reviews.MapGet("/screenings", ListAsync).WithSummary("Screenings, newest first; ?status=PendingReview for the review queue");
        reviews.MapPost("/screenings/{transferId:guid}/decision", DecideAsync).WithSummary("Approve or reject a transfer held for review");
    }

    private static async Task<Ok<List<ScreeningResponse>>> ListAsync(ScreeningStatus? status, IQuerySession session, CancellationToken cancellationToken)
    {
        IQueryable<Screening> query = session.Query<Screening>();
        if (status is { } wanted)
        {
            query = query.Where(screening => screening.Status == wanted);
        }

        var screenings = await query.OrderByDescending(screening => screening.RequestedAt).Take(100).ToListAsync(cancellationToken);
        var accounts = await session.Query<KnownAccount>().ToListAsync(cancellationToken);
        var byId = accounts.ToDictionary(account => account.Id);
        var byIban = accounts.ToDictionary(account => account.Iban, StringComparer.Ordinal);

        return TypedResults.Ok(screenings.Select(screening => new ScreeningResponse(
            screening.Id,
            byId.TryGetValue(screening.FromAccountId, out var from) ? from.Name : screening.FromAccountId.ToString(),
            screening.ToIban,
            byIban.GetValueOrDefault(screening.ToIban)?.Name,
            screening.AmountCents / 100m,
            screening.RequestedAt,
            screening.Decision,
            screening.Status,
            screening.Hits,
            screening.DecidedBy,
            screening.DecidedAt,
            screening.Comment)).ToList());
    }

    private static async Task<IResult> DecideAsync(Guid transferId, ReviewDecisionRequest request, ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<Result>(
            new DecideReview(transferId, request.Approve, user.Identity?.Name ?? user.UserId(), request.Comment),
            cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
    }
}
