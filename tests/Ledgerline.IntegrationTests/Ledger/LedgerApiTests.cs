using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ledgerline.Contracts;
using Ledgerline.IntegrationTests.Support;
using Ledgerline.Ledger;
using Ledgerline.Ledger.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Ledgerline.IntegrationTests.Ledger;

[Collection(InfrastructureGroup.Name)]
public sealed class LedgerApiTests(Infrastructure infrastructure) : IAsyncLifetime
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private ServiceFactory<LedgerService> _ledger = null!;

    public async ValueTask InitializeAsync()
    {
        _ledger = new ServiceFactory<LedgerService>("ledgerdb", await infrastructure.CreateDatabaseAsync("ledger"), await infrastructure.CreateVirtualHostAsync("ledger"), seed: true);
        await _ledger.Services.GetRequiredService<IHost>().WaitForSeedAsync();
    }

    public async ValueTask DisposeAsync() => await _ledger.DisposeAsync();

    [Fact]
    public async Task Customer_SeesOnlyTheirOwnAccounts_WithSeededBalances()
    {
        using var alice = Client(TestUsers.Customer(DemoAccounts.AliceUserId));

        var accounts = await alice.GetFromJsonAsync<List<AccountResponse>>("/api/ledger/accounts", Json, TestContext.Current.CancellationToken);

        accounts.ShouldNotBeNull();
        accounts.Select(a => a.Id).ShouldBe([DemoAccounts.AliceCurrent, DemoAccounts.AliceSavings], ignoreOrder: true);
        accounts.Single(a => a.Id == DemoAccounts.AliceCurrent).Balance.ShouldBe(6_400m);
        (await alice.GetAsync($"/api/ledger/accounts/{DemoAccounts.BobCurrent}", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReserveThenCapture_MovesMoney_AndKeepsTheBooksBalanced()
    {
        var transferId = Guid.CreateVersion7();
        var host = _ledger.Services.GetRequiredService<IHost>();

        var (_, reserved) = await host.InvokeMessageAndWaitAsync<FundsReserved>(new ReserveFunds(transferId, DemoAccounts.AliceCurrent, 12_345, "Loyer"));
        var (_, captured) = await host.InvokeMessageAndWaitAsync<TransferCaptured>(new CaptureTransfer(transferId, DemoAccounts.AliceCurrent, DemoAccounts.BobCurrent, 12_345, "Loyer"));

        reserved.ShouldNotBeNull();
        captured.ShouldNotBeNull();
        using var ops = Client(TestUsers.Operator);
        var trial = await ops.GetFromJsonAsync<TrialBalanceResponse>("/api/ledger/trial-balance", Json, TestContext.Current.CancellationToken);
        trial!.Balanced.ShouldBeTrue();
        trial.Accounts.Single(a => a.AccountId == DemoAccounts.BobCurrent).Balance.ShouldBe(3_823.45m);
        var journal = await ops.GetFromJsonAsync<List<JournalEntryResponse>>("/api/ledger/journal", Json, TestContext.Current.CancellationToken);
        var entry = journal!.Single(e => e.Id == transferId);
        entry.Balanced.ShouldBeTrue();
        entry.Lines.Sum(l => l.Debit).ShouldBe(123.45m);
        entry.Lines.Sum(l => l.Credit).ShouldBe(123.45m);
    }

    [Fact]
    public async Task ReservingMoreThanAvailable_IsRejected_WithACode()
    {
        var host = _ledger.Services.GetRequiredService<IHost>();

        var (_, rejected) = await host.InvokeMessageAndWaitAsync<FundsReservationRejected>(new ReserveFunds(Guid.CreateVersion7(), DemoAccounts.ChloeCurrent, 99_999_999, "Too much"));

        rejected.ShouldNotBeNull().Code.ShouldBe("ledger.insufficient_funds");
    }

    [Fact]
    public async Task RedeliveredCommands_DoNotMoveMoneyTwice()
    {
        var transferId = Guid.CreateVersion7();
        var host = _ledger.Services.GetRequiredService<IHost>();
        var reserve = new ReserveFunds(transferId, DemoAccounts.BobCurrent, 5_000, "Dup");
        var capture = new CaptureTransfer(transferId, DemoAccounts.BobCurrent, DemoAccounts.ChloeCurrent, 5_000, "Dup");

        await host.InvokeMessageAndWaitAsync<FundsReserved>(reserve);
        await host.InvokeMessageAndWaitAsync<FundsReserved>(reserve);
        await host.InvokeMessageAndWaitAsync<TransferCaptured>(capture);
        await host.InvokeMessageAndWaitAsync<TransferCaptured>(capture);

        using var chloe = Client(TestUsers.Customer(DemoAccounts.ChloeUserId));
        var account = await chloe.GetFromJsonAsync<AccountResponse>($"/api/ledger/accounts/{DemoAccounts.ChloeCurrent}", Json, TestContext.Current.CancellationToken);
        account!.Balance.ShouldBe(690m);
        account.Holds.ShouldBeEmpty();
    }

    [Fact]
    public async Task BalanceAsOf_ReplaysTheStreamUpToAValueDate()
    {
        using var alice = Client(TestUsers.Customer(DemoAccounts.AliceUserId));
        var tenDaysAgo = DateTimeOffset.UtcNow.AddDays(-10).ToString("O");

        var past = await alice.GetFromJsonAsync<BalanceAsOfResponse>($"/api/ledger/accounts/{DemoAccounts.AliceCurrent}/balance?asOf={Uri.EscapeDataString(tenDaysAgo)}", Json, TestContext.Current.CancellationToken);

        past!.Balance.ShouldBe(3_200m);
    }

    [Fact]
    public async Task IbanLookup_RevealsOnlyTheHolderName()
    {
        using var alice = Client(TestUsers.Customer(DemoAccounts.AliceUserId));
        using var bob = Client(TestUsers.Customer(DemoAccounts.BobUserId));
        var bobAccount = (await bob.GetFromJsonAsync<List<AccountResponse>>("/api/ledger/accounts", Json, TestContext.Current.CancellationToken))!.Single();

        var found = await alice.GetFromJsonAsync<IbanLookupResponse>($"/api/ledger/accounts/lookup?iban={Uri.EscapeDataString(bobAccount.IbanFormatted)}", Json, TestContext.Current.CancellationToken);

        found!.Exists.ShouldBeTrue();
        found.Name.ShouldBe("Bob Durand — Compte courant");
    }

    [Fact]
    public async Task OperationsEndpoints_AreForOperatorsOnly()
    {
        using var alice = Client(TestUsers.Customer(DemoAccounts.AliceUserId));
        using var anonymous = _ledger.CreateClient();

        (await alice.GetAsync("/api/ledger/trial-balance", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await anonymous.GetAsync("/api/ledger/accounts", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FrozenAccount_RefusesReservations_UntilAnOperatorUnfreezesIt()
    {
        var host = _ledger.Services.GetRequiredService<IHost>();
        using var ops = Client(TestUsers.Operator);
        using var chloe = Client(TestUsers.Customer(DemoAccounts.ChloeUserId));
        var freezeUrl = $"/api/ledger/accounts/{DemoAccounts.ChloeCurrent}/freeze";

        (await chloe.PostAsJsonAsync(freezeUrl, new FreezeRequest("self-service"), TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ops.PostAsJsonAsync(freezeUrl, new FreezeRequest(""), TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ops.PostAsJsonAsync(freezeUrl, new FreezeRequest("Suspected account takeover"), TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var frozen = await chloe.GetFromJsonAsync<AccountResponse>($"/api/ledger/accounts/{DemoAccounts.ChloeCurrent}", Json, TestContext.Current.CancellationToken);
        frozen!.Frozen.ShouldBeTrue();
        frozen.FrozenReason.ShouldBe("Suspected account takeover");
        var (_, rejected) = await host.InvokeMessageAndWaitAsync<FundsReservationRejected>(new ReserveFunds(Guid.CreateVersion7(), DemoAccounts.ChloeCurrent, 100, "Coffee"));
        rejected.ShouldNotBeNull().Code.ShouldBe("ledger.account_frozen");

        (await ops.PostAsync($"/api/ledger/accounts/{DemoAccounts.ChloeCurrent}/unfreeze", null, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var (_, reserved) = await host.InvokeMessageAndWaitAsync<FundsReserved>(new ReserveFunds(Guid.CreateVersion7(), DemoAccounts.ChloeCurrent, 100, "Coffee"));
        reserved.ShouldNotBeNull();

        var history = await chloe.GetFromJsonAsync<List<AccountEventResponse>>($"/api/ledger/accounts/{DemoAccounts.ChloeCurrent}/history", Json, TestContext.Current.CancellationToken);
        history.ShouldNotBeNull();
        history.Select(e => e.Type).Take(3).ShouldBe(["held", "unfrozen", "frozen"]);
        history[^1].Type.ShouldBe("opened");
        history.Single(e => e.Type == "frozen").Detail.ShouldBeNull();
    }

    [Fact]
    public async Task Movements_SplitMoneyInAndOut_AcrossTheCallersAccountsOnly()
    {
        var host = _ledger.Services.GetRequiredService<IHost>();
        var transferId = Guid.CreateVersion7();
        await host.InvokeMessageAndWaitAsync<FundsReserved>(new ReserveFunds(transferId, DemoAccounts.AliceCurrent, 4_200, "Courses"));
        await host.InvokeMessageAndWaitAsync<TransferCaptured>(new CaptureTransfer(transferId, DemoAccounts.AliceCurrent, DemoAccounts.BobCurrent, 4_200, "Courses"));
        using var alice = Client(TestUsers.Customer(DemoAccounts.AliceUserId));

        var all = await alice.GetFromJsonAsync<MovementsResponse>("/api/ledger/movements", Json, TestContext.Current.CancellationToken);
        var outgoing = await alice.GetFromJsonAsync<MovementsResponse>("/api/ledger/movements?direction=out", Json, TestContext.Current.CancellationToken);

        all!.Lines.Select(l => l.AccountId).Distinct().ShouldBeSubsetOf([DemoAccounts.AliceCurrent, DemoAccounts.AliceSavings]);
        all.Net.ShouldBe(all.MoneyIn - all.MoneyOut);
        all.Months.Sum(m => m.MoneyIn).ShouldBe(all.MoneyIn);
        outgoing!.Lines.ShouldAllBe(l => l.Amount < 0);
        outgoing.Lines.ShouldContain(l => l.EntryId == transferId && l.Amount == -42m && l.Counterparty == "Bob Durand — Compte courant");
        outgoing.MoneyIn.ShouldBe(all.MoneyIn);
        (await alice.GetAsync("/api/ledger/movements?direction=sideways", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private HttpClient Client(System.Net.Http.Headers.AuthenticationHeaderValue auth)
    {
        var client = _ledger.CreateClient();
        client.DefaultRequestHeaders.Authorization = auth;
        return client;
    }
}
