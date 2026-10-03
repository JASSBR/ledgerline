using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Routes live in code: they are the system's public API surface and are reviewed like code.
// Cluster addresses use service discovery ("https+http://ledger"): Aspire locally, Container Apps DNS in Azure.
RouteConfig[] routes =
[
    new() { RouteId = "ledger", ClusterId = "ledger", Match = new() { Path = "/api/ledger/{**rest}" } },
    new() { RouteId = "payments", ClusterId = "payments", Match = new() { Path = "/api/payments/{**rest}" } },
    new() { RouteId = "transfers-hub", ClusterId = "payments", Match = new() { Path = "/hubs/{**rest}" } },
    new() { RouteId = "fraud", ClusterId = "fraud", Match = new() { Path = "/api/fraud/{**rest}" } },
];
ClusterConfig[] clusters =
[
    Cluster("ledger", builder.Configuration["Services:Ledger"] ?? "https+http://ledger"),
    Cluster("payments", builder.Configuration["Services:Payments"] ?? "https+http://payments"),
    Cluster("fraud", builder.Configuration["Services:Fraud"] ?? "https+http://fraud"),
];
builder.Services.AddReverseProxy().LoadFromMemory(routes, clusters).AddServiceDiscoveryDestinationResolver();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .WithExposedHeaders("Location", "Idempotent-Replayed")));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Per client IP, at the edge: abuse is stopped before it reaches any service.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimiting:PermitPerMinute", 300), Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto,
});
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers.XFrameOptions = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    await next(context);
});
app.UseCors();
app.UseRateLimiter();
app.UseWebSockets();
app.MapDefaultEndpoints();
app.MapReverseProxy();

await app.RunAsync();

static ClusterConfig Cluster(string id, string address) => new()
{
    ClusterId = id,
    Destinations = new Dictionary<string, DestinationConfig>(StringComparer.Ordinal) { [id] = new() { Address = address } },
};
