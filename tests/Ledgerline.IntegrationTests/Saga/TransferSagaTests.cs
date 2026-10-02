using System.Net;
using System.Net.Http.Json;
using Ledgerline.Fraud;
using Ledgerline.Fraud.Endpoints;
using Ledgerline.IntegrationTests.Support;
using Ledgerline.Ledger;
using Ledgerline.Ledger.Endpoints;
using Ledgerline.Payments.Domain;
using Ledgerline.Payments.Endpoints;

namespace Ledgerline.IntegrationTests.Saga;

[Collection(BankGroup.Name)]
public sealed class TransferSagaTests(BankFixture bank)
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = LedgerJson.Options;

    [Fact]
    public async Task SmallTransfer_IsClearedAutomatically_AndMovesMoneyInTheBooks()
    {
        var before = await AccountAsync(DemoAccounts.BobCurrent);

        var transfer = await RunAsync(DemoAccounts.AliceUserId, DemoAccounts.AliceCurrent, DemoAccounts.BobCurrent, 25m);

        transfer.Status.ShouldBe(TransferStatus.Completed);
        transfer.Steps.Select(step => step.Status).ShouldBe([TransferStatus.Reserving, TransferStatus.Screening, TransferStatus.Capturing, TransferStatus.Completed]);
        (await AccountAsync(DemoAccounts.BobCurrent)).Balance.ShouldBe(before.Balance + 25m);
        (await TrialBalanceAsync()).Balanced.ShouldBeTrue();
    }

    [Fact]
    public async Task LargeTransfer_WaitsForAnAnalyst_WithFundsReserved_ThenCompletesOnApproval()
    {
        var aliceBefore = await AccountAsync(DemoAccounts.AliceCurrent);
        var transfer = await RequestAsync(DemoAccounts.AliceUserId, DemoAccounts.AliceCurrent, DemoAccounts.BobCurrent, 3_500m);
        await WaitForAsync(transfer.Id, TransferStatus.PendingReview);

        (await AccountAsync(DemoAccounts.AliceCurrent)).Available.ShouldBe(aliceBefore.Available - 3_500m);
        var review = (await ScreeningsAsync()).Single(s => s.TransferId == transfer.Id);
        review.Hits.ShouldContain(hit => string.Equals(hit.Rule, "large-amount", StringComparison.Ordinal));

        await DecideAsync(transfer.Id, approve: true, comment: null);
        var done = await WaitForAsync(transfer.Id, TransferStatus.Completed);

        done.Steps.ShouldContain(step => step.Status == TransferStatus.Capturing && string.Equals(step.Detail, "Approved by operator-1", StringComparison.Ordinal));
        (await AccountAsync(DemoAccounts.AliceCurrent)).Balance.ShouldBe(aliceBefore.Balance - 3_500m);
    }

    [Fact]
    public async Task AnalystRejection_ReleasesTheReservedFunds()
    {
        var savingsBefore = await AccountAsync(DemoAccounts.AliceSavings);
        var transfer = await RequestAsync(DemoAccounts.AliceUserId, DemoAccounts.AliceSavings, DemoAccounts.BobCurrent, 3_200m);
        await WaitForAsync(transfer.Id, TransferStatus.PendingReview);

        await DecideAsync(transfer.Id, approve: false, comment: "Beneficiary could not be verified");
        var rejected = await WaitForAsync(transfer.Id, TransferStatus.Rejected);

        rejected.Reason.ShouldBe("Beneficiary could not be verified");
        var savings = await AccountAsync(DemoAccounts.AliceSavings);
        savings.Available.ShouldBe(savingsBefore.Available);
        savings.Holds.ShouldBeEmpty();
    }

    [Fact]
    public async Task BlocklistedBeneficiary_IsBlocked_AndCompensated()
    {
        var bobBefore = await AccountAsync(DemoAccounts.BobCurrent);

        var transfer = await RunAsync(DemoAccounts.BobUserId, DemoAccounts.BobCurrent, DemoAccounts.MuleAccount, 10m);

        transfer.Status.ShouldBe(TransferStatus.Rejected);
        transfer.Reason!.ShouldContain("blocklist");
        transfer.Steps.Select(step => step.Status).ShouldContain(TransferStatus.Releasing);
        (await AccountAsync(DemoAccounts.BobCurrent)).Available.ShouldBe(bobBefore.Available);
    }

    [Fact]
    public async Task InsufficientFunds_FailsAtReservation_BeforeAnyScreening()
    {
        var transfer = await RunAsync(DemoAccounts.ChloeUserId, DemoAccounts.ChloeCurrent, DemoAccounts.AliceCurrent, 900m);

        transfer.Status.ShouldBe(TransferStatus.Failed);
        transfer.Reason!.ShouldContain("Insufficient");
        transfer.Steps.Select(step => step.Status).ShouldNotContain(TransferStatus.Screening);
    }

    [Fact]
    public async Task SameIdempotencyKey_ReturnsTheOriginalTransfer_AndRefusesADifferentRequest()
    {
        using var alice = bank.Payments.CustomerClient(DemoAccounts.AliceUserId);
        var key = Guid.NewGuid().ToString();
        var body = new TransferRequest(DemoAccounts.AliceCurrent, bank.Ibans[DemoAccounts.BobCurrent], 12m, "Retry me");

        var first = await PostAsync(alice, body, key);
        var retry = await PostAsync(alice, body, key);
        var conflicting = await PostAsync(alice, body with { Amount = 13m }, key);
        var original = (await first.Content.ReadFromJsonAsync<TransferResponse>(Json, TestContext.Current.CancellationToken))!;
        // Let the accepted transfer finish, so it cannot move Alice's balance under another test's feet.
        await WaitForTerminalAsync(original.Id);

        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        retry.StatusCode.ShouldBe(HttpStatusCode.OK);
        retry.Headers.GetValues("Idempotent-Replayed").ShouldBe(["true"]);
        (await retry.Content.ReadFromJsonAsync<TransferResponse>(Json, TestContext.Current.CancellationToken))!.Id.ShouldBe(original.Id);
        conflicting.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await PostAsync(alice, body, idempotencyKey: null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnreviewedTransfer_IsReleased_WhenTheReviewDeadlinePasses()
    {
        var bobBefore = await AccountAsync(DemoAccounts.BobCurrent);
        var transfer = await RequestAsync(DemoAccounts.BobUserId, DemoAccounts.BobCurrent, DemoAccounts.AliceCurrent, 3_000m);
        await WaitForAsync(transfer.Id, TransferStatus.PendingReview);

        var expired = await WaitForAsync(transfer.Id, TransferStatus.Rejected, BankFixture.ReviewDeadline + TimeSpan.FromSeconds(15));

        expired.Reason.ShouldBe("Review deadline passed");
        (await AccountAsync(DemoAccounts.BobCurrent)).Available.ShouldBe(bobBefore.Available);
        (await ScreeningsAsync()).Single(s => s.TransferId == transfer.Id).Status.ShouldBe(ScreeningStatus.PendingReview);
    }

    /// <summary>
    /// Twenty transfers race for the same 640 €. Optimistic concurrency on the account stream plus Wolverine retries
    /// serialise them: exactly twelve fit, eight fail, and the account never goes below zero.
    /// </summary>
    [Fact]
    public async Task ConcurrentTransfers_NeverOverdrawTheAccount()
    {
        var requests = Enumerable.Range(0, 20)
            .Select(_ => RequestAsync(DemoAccounts.ChloeUserId, DemoAccounts.ChloeCurrent, DemoAccounts.AliceSavings, 50m))
            .ToList();
        var accepted = await Task.WhenAll(requests);

        var outcomes = await Task.WhenAll(accepted.Select(t => WaitForTerminalAsync(t.Id)));

        outcomes.Count(t => t.Status == TransferStatus.Completed).ShouldBe(12);
        outcomes.Count(t => t.Status == TransferStatus.Failed).ShouldBe(8);
        var chloe = await AccountAsync(DemoAccounts.ChloeCurrent);
        chloe.Balance.ShouldBe(40m);
        chloe.Holds.ShouldBeEmpty();
        (await TrialBalanceAsync()).Balanced.ShouldBeTrue();
    }

    // ---------------------------------------------------------------- helpers

    private async Task<TransferResponse> RunAsync(string userId, Guid from, Guid to, decimal amount)
    {
        var transfer = await RequestAsync(userId, from, to, amount);
        return await WaitForTerminalAsync(transfer.Id);
    }

    private async Task<TransferResponse> RequestAsync(string userId, Guid from, Guid to, decimal amount)
    {
        using var client = bank.Payments.CustomerClient(userId);
        var response = await PostAsync(client, new TransferRequest(from, bank.Ibans[to], amount, null), Guid.NewGuid().ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<TransferResponse>(Json))!;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, TransferRequest body, string? idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/transfers") { Content = JsonContent.Create(body, options: Json) };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return await client.SendAsync(request);
    }

    private Task<TransferResponse> WaitForTerminalAsync(Guid id) =>
        PollAsync(id, transfer => transfer.Status.IsTerminal(), TimeSpan.FromSeconds(30));

    private Task<TransferResponse> WaitForAsync(Guid id, TransferStatus status, TimeSpan? timeout = null) =>
        PollAsync(id, transfer => transfer.Status == status, timeout ?? TimeSpan.FromSeconds(20));

    private async Task<TransferResponse> PollAsync(Guid id, Func<TransferResponse, bool> done, TimeSpan timeout)
    {
        using var ops = BankFixture.Client(bank.Payments, TestUsers.Operator);
        var deadline = DateTime.UtcNow + timeout;
        TransferResponse? last = null;
        while (DateTime.UtcNow < deadline)
        {
            last = await ops.GetFromJsonAsync<TransferResponse>($"/api/payments/transfers/{id}", Json);
            if (last is not null && done(last))
            {
                return last;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Transfer {id} stuck in {last?.Status}: {string.Join(" → ", last?.Steps.Select(s => s.Status) ?? [])}");
    }

    private async Task<AccountResponse> AccountAsync(Guid id)
    {
        using var ops = BankFixture.Client(bank.Ledger, TestUsers.Operator);
        return (await ops.GetFromJsonAsync<AccountResponse>($"/api/ledger/accounts/{id}", Json))!;
    }

    private async Task<TrialBalanceResponse> TrialBalanceAsync()
    {
        using var ops = BankFixture.Client(bank.Ledger, TestUsers.Operator);
        return (await ops.GetFromJsonAsync<TrialBalanceResponse>("/api/ledger/trial-balance", Json))!;
    }

    private async Task<List<ScreeningResponse>> ScreeningsAsync()
    {
        using var ops = BankFixture.Client(bank.Fraud, TestUsers.Operator);
        return (await ops.GetFromJsonAsync<List<ScreeningResponse>>("/api/fraud/screenings", Json))!;
    }

    private async Task DecideAsync(Guid transferId, bool approve, string? comment)
    {
        using var ops = BankFixture.Client(bank.Fraud, TestUsers.Operator);
        var response = await ops.PostAsJsonAsync($"/api/fraud/screenings/{transferId}/decision", new ReviewDecisionRequest(approve, comment), Json);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }
}

internal static class FactoryClients
{
    public static HttpClient CustomerClient<T>(this ServiceFactory<T> service, string userId)
        where T : class => BankFixture.Client(service, TestUsers.Customer(userId));
}
