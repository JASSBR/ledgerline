using System.Reflection;
using Ledgerline.Contracts;
using Ledgerline.Payments.Domain;
using Ledgerline.Payments.Handlers;
using Marten;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Ledgerline.Payments.Tests;

/// <summary>
/// The saga is a plain class: each step can be exercised without a broker or a database. These tests pin the
/// orchestration — what it sends next — and the guards that make late or duplicate replies harmless.
/// </summary>
public sealed class TransferSagaTests
{
    private static readonly TimeProvider Time = TimeProvider.System;
    private readonly IDocumentSession _session = NullSession.Create();

    private static Transfer Started(out ReserveFunds reserve)
    {
        (var saga, reserve) = Transfer.Start(new StartTransfer(Guid.CreateVersion7(), "alice", Guid.CreateVersion7(), Guid.CreateVersion7(), "FR76…", 12_000, "Loyer"));
        return saga;
    }

    [Fact]
    public void Start_ReservesTheFundsFirst()
    {
        var saga = Started(out var reserve);

        saga.Status.ShouldBe(TransferStatus.Reserving);
        reserve.TransferId.ShouldBe(saga.Id);
        reserve.AmountCents.ShouldBe(12_000);
    }

    [Fact]
    public async Task HappyPath_Reserve_Screen_Capture_Complete()
    {
        var saga = Started(out _);

        (await saga.Handle(new FundsReserved(saga.Id), _session, Time)).ShouldContain(m => m is ScreenTransfer);
        (await saga.Handle(new TransferCleared(saga.Id, [], "rules"), _session, Time)).ShouldContain(m => m is CaptureTransfer);
        var last = await saga.Handle(new TransferCaptured(saga.Id, DateTimeOffset.UtcNow), _session, Time);

        saga.Status.ShouldBe(TransferStatus.Completed);
        saga.IsCompleted().ShouldBeTrue();
        last.OfType<TransferStatusChanged>().ShouldHaveSingleItem().Status.ShouldBe("Completed");
    }

    [Fact]
    public async Task Blocked_TriggersTheCompensation_ThenEndsRejected()
    {
        var saga = Started(out _);
        await saga.Handle(new FundsReserved(saga.Id), _session, Time);

        var compensation = await saga.Handle(new TransferBlocked(saga.Id, "blocklist", ["blocked-beneficiary"], "rules"), _session, Time);
        compensation.OfType<ReleaseFunds>().ShouldHaveSingleItem().AccountId.ShouldBe(saga.FromAccountId);
        await saga.Handle(new FundsReleased(saga.Id), _session, Time);

        saga.Status.ShouldBe(TransferStatus.Rejected);
    }

    [Fact]
    public async Task FailedCapture_IsCompensated_AndEndsFailed()
    {
        var saga = Started(out _);
        await saga.Handle(new FundsReserved(saga.Id), _session, Time);
        await saga.Handle(new TransferCleared(saga.Id, [], "rules"), _session, Time);

        (await saga.Handle(new TransferCaptureFailed(saga.Id, "ledger.account_not_found", "gone"), _session, Time)).ShouldContain(m => m is ReleaseFunds);
        await saga.Handle(new FundsReleased(saga.Id), _session, Time);

        saga.Status.ShouldBe(TransferStatus.Failed);
    }

    [Fact]
    public async Task Review_SchedulesADeadline_ThatReleasesTheFundsIfNobodyDecides()
    {
        var saga = Started(out _);
        await saga.Handle(new FundsReserved(saga.Id), _session, Time);

        var held = await saga.Handle(new TransferHeldForReview(saga.Id, "large", ["large-amount"]), _session, Time, Options.Create(new PaymentsOptions()));
        held.ShouldContain(m => m is DeliveryMessage<ReviewDeadlinePassed>);
        (await saga.Handle(new ReviewDeadlinePassed(saga.Id), _session, Time)).ShouldContain(m => m is ReleaseFunds);

        saga.Status.ShouldBe(TransferStatus.Releasing);
    }

    [Fact]
    public async Task DuplicateAndLateReplies_AreIgnored()
    {
        var saga = Started(out _);
        await saga.Handle(new FundsReserved(saga.Id), _session, Time);

        (await saga.Handle(new FundsReserved(saga.Id), _session, Time)).ShouldBeEmpty();
        (await saga.Handle(new TransferCaptured(saga.Id, DateTimeOffset.UtcNow), _session, Time)).ShouldBeEmpty();
        (await saga.Handle(new ReviewDeadlinePassed(saga.Id), _session, Time)).ShouldBeEmpty();
        saga.Status.ShouldBe(TransferStatus.Screening);
    }
}

/// <summary>
/// A do-nothing IDocumentSession: the saga only loads and stores its customer-facing view, which these tests ignore.
/// Every call returns the neutral value of its return type.
/// </summary>
public class NullSession : DispatchProxy
{
    public static IDocumentSession Create() => Create<IDocumentSession, NullSession>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var type = targetMethod!.ReturnType;
        if (type == typeof(void))
        {
            return null;
        }

        if (type == typeof(Task))
        {
            return Task.CompletedTask;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var result = type.GetGenericArguments()[0];
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(result)
                .Invoke(null, [result.IsValueType ? Activator.CreateInstance(result) : null]);
        }

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
