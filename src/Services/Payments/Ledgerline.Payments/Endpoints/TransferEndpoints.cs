using System.Security.Claims;
using Ledgerline.Hosting;
using Ledgerline.Payments.Domain;
using Ledgerline.Payments.Handlers;
using Ledgerline.SharedKernel;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Ledgerline.Payments.Endpoints;

public sealed record TransferResponse(
    Guid Id,
    Guid FromAccountId,
    string FromName,
    string ToIban,
    string ToName,
    decimal Amount,
    string Label,
    TransferStatus Status,
    string? Reason,
    DateTimeOffset RequestedAt,
    IReadOnlyList<TransferStep> Steps)
{
    internal static TransferResponse From(TransferView view) => new(
        view.Id, view.FromAccountId, view.FromName, view.ToIban, view.ToName, view.AmountCents / 100m,
        view.Label, view.Status, view.Reason, view.RequestedAt, view.Steps);
}

internal static class TransferEndpoints
{
    public const string IdempotencyHeader = "Idempotency-Key";

    public static void Map(RouteGroupBuilder api)
    {
        var transfers = api.MapGroup("/transfers").WithTags("Transfers").RequireAuthorization();
        transfers.MapPost("/", RequestAsync)
            .RequireAuthorization(LedgerlineServiceExtensions.CustomerPolicy)
            .WithSummary("Request a transfer. Requires an Idempotency-Key header; a retry with the same key returns the original transfer.");
        transfers.MapGet("/", ListAsync).WithSummary("Your transfers, newest first (all transfers for operators)");
        transfers.MapGet("/{id:guid}", GetAsync).WithSummary("A transfer with its full timeline");
    }

    private static async Task<IResult> RequestAsync(
        TransferRequest request,
        [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey,
        ClaimsPrincipal user,
        HttpContext http,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<Result<TransferAccepted>>(new RequestTransfer(user.UserId(), idempotencyKey ?? string.Empty, request), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!.ToProblem();
        }

        var response = TransferResponse.From(result.Value.Transfer);
        if (result.Value.Replayed)
        {
            // Same convention as Stripe's API: the client can tell a replay from a new transfer.
            http.Response.Headers["Idempotent-Replayed"] = "true";
            return TypedResults.Ok(response);
        }

        return TypedResults.Accepted($"/api/payments/transfers/{response.Id}", response);
    }

    private static async Task<Ok<List<TransferResponse>>> ListAsync(ClaimsPrincipal user, IQuerySession session, int? limit, CancellationToken cancellationToken)
    {
        IQueryable<TransferView> query = session.Query<TransferView>();
        if (!user.IsOperator())
        {
            var userId = user.UserId();
            query = query.Where(view => view.OwnerId == userId);
        }

        var views = await query.OrderByDescending(view => view.RequestedAt).Take(Math.Clamp(limit ?? 50, 1, 200)).ToListAsync(cancellationToken);
        return TypedResults.Ok(views.Select(TransferResponse.From).ToList());
    }

    private static async Task<IResult> GetAsync(Guid id, ClaimsPrincipal user, IQuerySession session, CancellationToken cancellationToken)
    {
        var view = await session.LoadAsync<TransferView>(id, cancellationToken);
        return view is not null && (user.IsOperator() || string.Equals(view.OwnerId, user.UserId(), StringComparison.Ordinal))
            ? TypedResults.Ok(TransferResponse.From(view))
            : PaymentErrors.TransferNotFound.ToProblem();
    }
}
