using JasperFx;
using Ledgerline.Hosting;
using Ledgerline.Payments;
using Ledgerline.Payments.Endpoints;
using Ledgerline.Payments.Handlers;
using Ledgerline.Payments.Realtime;
using Wolverine;
using Wolverine.ErrorHandling;

var builder = WebApplication.CreateBuilder(args);

builder.AddLedgerlineService(PaymentsService.Name, typeof(PaymentsService).Assembly, PaymentsStore.Configure);
builder.AddAccountDirectorySubscription(PaymentsService.Name);
builder.Services.AddOptions<PaymentsOptions>().Bind(builder.Configuration.GetSection(PaymentsOptions.SectionName));
builder.Services.AddSignalR().AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.ConfigureWolverine(options =>
    // Two requests with the same Idempotency-Key at the same instant: the loser retries and finds the winner's record.
    options.OnException<DocumentAlreadyExistsException>().RetryOnce());

var app = builder.Build();
app.UseLedgerlineService();
TransferEndpoints.Map(app.MapGroup("/api/payments"));
app.MapHub<TransfersHub>(TransfersHub.Path);

await app.RunAsync();
