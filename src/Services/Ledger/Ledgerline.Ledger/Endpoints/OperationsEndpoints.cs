using Ledgerline.Hosting;
using Ledgerline.Ledger.Domain;
using Ledgerline.Ledger.Handlers;
using Ledgerline.Ledger.Persistence;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine;

namespace Ledgerline.Ledger.Endpoints;

/// <summary>Back-office views of the books, for operators only.</summary>
internal static class OperationsEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var operations = api.MapGroup(string.Empty).WithTags("Operations").RequireAuthorization(LedgerlineServiceExtensions.OperatorPolicy);
        operations.MapGet("/journal", JournalAsync).WithSummary("Latest journal entries, as debit/credit lines");
        operations.MapGet("/trial-balance", TrialBalanceAsync).WithSummary("Every account balance, and proof that the books sum to zero");
        operations.MapPost("/accounts/{id:guid}/deposits", DepositAsync).WithSummary("Credit a customer account from the treasury");
    }

    private static async Task<Ok<List<JournalEntryResponse>>> JournalAsync(IQuerySession session, int? limit, CancellationToken cancellationToken)
    {
        var entries = await session.Query<JournalEntryView>()
            .OrderByDescending(entry => entry.PostedAt)
            .Take(Math.Clamp(limit ?? 50, 1, 200))
            .ToListAsync(cancellationToken);
        var names = await AccountEndpoints.NamesAsync(session, entries.SelectMany(entry => entry.Lines.Select(line => line.AccountId)), cancellationToken);

        return TypedResults.Ok(entries.Select(entry => new JournalEntryResponse(
            entry.Id,
            entry.Reference,
            entry.PostedAt,
            [.. entry.Lines.OrderBy(line => line.AmountCents).Select(line => new JournalLineResponse(
                line.AccountId,
                names.GetValueOrDefault(line.AccountId, "?"),
                line.AmountCents < 0 ? new Money(-line.AmountCents).ToEuros() : 0,
                line.AmountCents > 0 ? new Money(line.AmountCents).ToEuros() : 0))],
            entry.TotalCents == 0)).ToList());
    }

    private static async Task<Ok<TrialBalanceResponse>> TrialBalanceAsync(IQuerySession session, CancellationToken cancellationToken)
    {
        var accounts = await session.Query<Account>().OrderBy(account => account.Kind).ThenBy(account => account.Name).ToListAsync(cancellationToken);
        var total = new Money(accounts.Sum(account => account.Balance.Cents));
        var entries = await session.Query<JournalEntryView>().CountAsync(cancellationToken);
        return TypedResults.Ok(new TrialBalanceResponse(
            [.. accounts.Select(account => new TrialBalanceLine(account.Id, account.Name, account.Kind, account.Balance.ToEuros()))],
            total.ToEuros(),
            total == Money.Zero,
            entries));
    }

    private static async Task<IResult> DepositAsync(Guid id, DepositRequest request, IMessageBus bus, TimeProvider time, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0 || decimal.Round(request.Amount, 2) != request.Amount)
        {
            return LedgerErrors.AmountNotPositive.ToProblem();
        }

        await bus.InvokeAsync(
            new PostDeposit(Guid.CreateVersion7(), id, Money.FromEuros(request.Amount).Cents, request.Reference ?? "Dépôt", time.GetUtcNow()),
            cancellationToken);
        return TypedResults.Accepted($"/api/ledger/accounts/{id}");
    }
}
