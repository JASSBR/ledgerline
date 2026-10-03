using Ledgerline.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.RabbitMQ;

namespace Ledgerline.Hosting;

public static class AccountDirectorySubscription
{
    /// <summary>The Ledger's conventional listener queue for the request ("{service}.{message type}").</summary>
    private const string LedgerRequestQueue = $"ledger.{nameof(RequestAccountDirectory)}";

    /// <summary>
    /// For services that keep a copy of the Ledger's accounts: asks for the full directory at every start.
    /// The request goes to the Ledger's queue, declared here too, so it waits there even if the Ledger is not up yet.
    /// </summary>
    public static WebApplicationBuilder AddAccountDirectorySubscription(this WebApplicationBuilder builder, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.ConfigureWolverine(options => options.PublishMessage<RequestAccountDirectory>().ToRabbitQueue(LedgerRequestQueue));
        builder.Services.AddHostedService(provider => new DirectoryRequest(provider.GetRequiredService<IServiceScopeFactory>(), serviceName));
        return builder;
    }

    private sealed class DirectoryRequest(IServiceScopeFactory scopes, string serviceName) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new RequestAccountDirectory(serviceName));
        }
    }
}
