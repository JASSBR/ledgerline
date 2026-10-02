using System.Security.Claims;
using System.Text.Json.Serialization;
using JasperFx;
using JasperFx.Events;
using Ledgerline.Contracts;
using Marten;
using Marten.Exceptions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using Weasel.Core;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Marten;
using Wolverine.RabbitMQ;

namespace Ledgerline.Hosting;

public static class LedgerlineServiceExtensions
{
    public const string MessagingConnectionName = "messaging";
    public const string CustomerRole = "customer";
    public const string OperatorRole = "operator";
    public const string CustomerPolicy = "customer";
    public const string OperatorPolicy = "operator";

    private static readonly TimeSpan[] ConcurrencyBackoff =
        [.. new[] { 10, 25, 50, 100, 200, 300, 500, 800, 1_000, 1_500 }.Select(ms => TimeSpan.FromMilliseconds(ms))];

    /// <summary>
    /// The shared skeleton of a Ledgerline service:
    /// Marten (documents + event store) on the service's own database and schema, Wolverine with a transactional
    /// inbox/outbox stored in that same database (a message is sent if and only if the business change commits),
    /// RabbitMQ routing by message type, Keycloak-issued JWTs and RFC 9457 errors.
    /// </summary>
    /// <param name="serviceAssembly">
    /// Where Wolverine looks for handlers. Explicit, because the entry assembly is not the service under test hosts.
    /// </param>
    public static WebApplicationBuilder AddLedgerlineService(
        this WebApplicationBuilder builder,
        string serviceName,
        System.Reflection.Assembly serviceAssembly,
        Action<StoreOptions> configureStore)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(configureStore);

        builder.AddServiceDefaults();
        builder.Services.AddProblemDetails();
        builder.Services.AddOpenApi();
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
        builder.Services.AddSingleton(TimeProvider.System);

        AddPersistence(builder, serviceName, configureStore);
        AddMessaging(builder, serviceName, serviceAssembly);
        AddSecurity(builder);
        return builder;
    }

    public static WebApplication UseLedgerlineService(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.MapDefaultEndpoints();
        return app;
    }

    private static void AddPersistence(WebApplicationBuilder builder, string serviceName, Action<StoreOptions> configureStore) =>
        builder.Services.AddMarten(provider =>
            {
                var options = new StoreOptions();
                options.Connection(provider.GetRequiredService<IConfiguration>().GetConnectionString($"{serviceName}db")
                    ?? throw new InvalidOperationException($"Connection string '{serviceName}db' is missing."));
                options.DatabaseSchemaName = serviceName;
                options.UseSystemTextJsonForSerialization(EnumStorage.AsString);
                options.Events.StreamIdentity = StreamIdentity.AsGuid;
                configureStore(options);
                return options;
            })
            .UseLightweightSessions()
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

    private static void AddMessaging(WebApplicationBuilder builder, string serviceName, System.Reflection.Assembly serviceAssembly) =>
        builder.UseWolverine(options =>
        {
            options.ServiceName = serviceName;
            options.ApplicationAssembly = serviceAssembly;
            options.Policies.AutoApplyTransactions();
            options.Policies.UseDurableLocalQueues();
            options.Policies.UseDurableInboxOnAllListeners();
            options.Policies.UseDurableOutboxOnAllSendingEndpoints();

            // Two writers on the same account stream: the loser retries against fresh state instead of failing.
            // Backoff grows so that a burst of payments on one account (the concurrency test fires 20 at once) drains.
            options.OnException<ConcurrencyException>().RetryWithCooldown(ConcurrencyBackoff);
            options.OnException<EventStreamUnexpectedMaxEventIdException>().RetryWithCooldown(ConcurrencyBackoff);

            var rabbit = builder.Configuration.GetConnectionString(MessagingConnectionName)
                ?? throw new InvalidOperationException($"Connection string '{MessagingConnectionName}' is missing.");
            var contractsNamespace = typeof(AccountRegistered).Namespace;
            options.UseRabbitMq(new Uri(rabbit))
                .AutoProvision()
                // One exchange per contract type; one queue per (service, type). Two services handling the same
                // event each get every message instead of competing for it.
                .UseConventionalRouting(convention => convention
                    .IncludeTypes(type => string.Equals(type.Namespace, contractsNamespace, StringComparison.Ordinal))
                    .QueueNameForListener(type => $"{serviceName}.{type.Name}"));
        });

    private static void AddSecurity(WebApplicationBuilder builder)
    {
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.Authority = builder.Configuration["Auth:Authority"];
                jwt.Audience = builder.Configuration["Auth:Audience"] ?? "ledgerline-api";
                jwt.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                // Keycloak's standard claims, kept raw. The realm adds a flat "roles" claim (see the realm export).
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters.NameClaimType = "preferred_username";
                jwt.TokenValidationParameters.RoleClaimType = "roles";
                jwt.Events = new JwtBearerEvents
                {
                    // Browsers cannot set headers on WebSockets: SignalR passes the token as a query parameter.
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                };
            });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(CustomerPolicy, policy => policy.RequireRole(CustomerRole))
            .AddPolicy(OperatorPolicy, policy => policy.RequireRole(OperatorRole));
    }

    /// <summary>The Keycloak user id ("sub") of the caller.</summary>
    public static string UserId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.FindFirstValue("sub") ?? throw new InvalidOperationException("Authenticated principal has no 'sub' claim.");
    }

    public static bool IsOperator(this ClaimsPrincipal user) => user?.IsInRole(OperatorRole) ?? false;
}
