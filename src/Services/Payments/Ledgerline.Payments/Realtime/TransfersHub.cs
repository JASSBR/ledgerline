using Ledgerline.Contracts;
using Ledgerline.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ledgerline.Payments.Realtime;

/// <summary>Pushes transfer progress: each customer to their own group, operators to theirs. Server → client only.</summary>
[Authorize]
public sealed class TransfersHub : Hub
{
    public const string Path = "/hubs/transfers";
    public const string OperatorsGroup = "operators";

    public static string UserGroup(string userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        var user = Context.User!;
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(user.UserId()));
        if (user.IsOperator())
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, OperatorsGroup);
        }

        await base.OnConnectedAsync();
    }
}

public static class TransferStatusChangedHandler
{
    public static Task Handle(TransferStatusChanged change, IHubContext<TransfersHub> hub, CancellationToken cancellationToken) =>
        hub.Clients.Groups(TransfersHub.UserGroup(change.OwnerId), TransfersHub.OperatorsGroup)
            .SendAsync("transferChanged", change, cancellationToken);
}

/// <summary>Euros for the browser, like every HTTP response: cents stay between services.</summary>
public sealed record TransferReceivedNotice(Guid TransferId, Guid ToAccountId, string FromName, decimal Amount, string Reference, DateTimeOffset ReceivedAt);

public static class TransferReceivedHandler
{
    public static Task Handle(TransferReceived received, IHubContext<TransfersHub> hub, CancellationToken cancellationToken) =>
        hub.Clients.Group(TransfersHub.UserGroup(received.BeneficiaryId)).SendAsync(
            "transferReceived",
            new TransferReceivedNotice(received.TransferId, received.ToAccountId, received.FromName, received.AmountCents / 100m, received.Reference, received.ReceivedAt),
            cancellationToken);
}
