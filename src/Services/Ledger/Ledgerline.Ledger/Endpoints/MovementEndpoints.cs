using System.Globalization;
using System.Security.Claims;
using Ledgerline.Hosting;
using Ledgerline.Ledger.Domain;
using Marten;

namespace Ledgerline.Ledger.Endpoints;

/// <summary>What came in and what went out, across all the caller's accounts.</summary>
internal static class MovementEndpoints
{
    private const int MaxLines = 500;

    public static void Map(RouteGroupBuilder api) =>
        api.MapGet("/movements", MovementsAsync)
            .WithTags("Accounts")
            .RequireAuthorization()
            .WithSummary("Money in and money out over a period, with monthly totals");

    private static async Task<IResult> MovementsAsync(
        ClaimsPrincipal user,
        IQuerySession session,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? direction,
        Guid? accountId,
        CancellationToken cancellationToken)
    {
        if (direction is not (null or "in" or "out"))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "direction must be 'in' or 'out'.");
        }

        var accounts = await VisibleCustomerAccountsAsync(user, session, cancellationToken);
        if (accountId is { } id)
        {
            accounts = [.. accounts.Where(account => account.Id == id)];
        }

        var postings = new List<(Account Account, EntryPosted Posted)>();
        foreach (var account in accounts)
        {
            var events = await session.Events.FetchStreamAsync(account.Id, token: cancellationToken);
            postings.AddRange(events.Select(e => e.Data).OfType<EntryPosted>()
                .Where(posted => (from is null || posted.PostedAt >= from) && (to is null || posted.PostedAt < to))
                .Select(posted => (account, posted)));
        }

        var moneyIn = new Money(postings.Where(p => p.Posted.AmountCents > 0).Sum(p => p.Posted.AmountCents));
        var moneyOut = new Money(-postings.Where(p => p.Posted.AmountCents < 0).Sum(p => p.Posted.AmountCents));
        var months = postings
            .GroupBy(p => p.Posted.PostedAt.UtcDateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new MonthlyFlowResponse(
                group.Key,
                new Money(group.Where(p => p.Posted.AmountCents > 0).Sum(p => p.Posted.AmountCents)).ToEuros(),
                new Money(-group.Where(p => p.Posted.AmountCents < 0).Sum(p => p.Posted.AmountCents)).ToEuros()))
            .ToList();

        var shown = postings
            .Where(p => direction switch { "in" => p.Posted.AmountCents > 0, "out" => p.Posted.AmountCents < 0, _ => true })
            .OrderByDescending(p => p.Posted.PostedAt)
            .Take(MaxLines)
            .ToList();
        var names = await AccountEndpoints.NamesAsync(session, shown.Select(p => p.Posted.CounterpartyAccountId), cancellationToken);

        return TypedResults.Ok(new MovementsResponse(
            moneyIn.ToEuros(),
            moneyOut.ToEuros(),
            (moneyIn - moneyOut).ToEuros(),
            months,
            [.. shown.Select(p => new MovementLineResponse(
                p.Posted.EntryId,
                p.Account.Id,
                p.Account.Name,
                p.Posted.PostedAt,
                p.Posted.Reference,
                new Money(p.Posted.AmountCents).ToEuros(),
                names.GetValueOrDefault(p.Posted.CounterpartyAccountId, "—")))]));
    }

    // Operators look across customers; the treasury's own lines would drown them (it is the other side of every deposit).
    private static async Task<IReadOnlyList<Account>> VisibleCustomerAccountsAsync(ClaimsPrincipal user, IQuerySession session, CancellationToken cancellationToken)
    {
        var query = session.Query<Account>().Where(account => account.Kind == AccountKind.Customer);
        return user.IsOperator()
            ? await query.ToListAsync(cancellationToken)
            : await query.Where(account => account.OwnerId == user.UserId()).ToListAsync(cancellationToken);
    }
}
