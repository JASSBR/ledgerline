using Ledgerline.Fraud;
using Ledgerline.Fraud.Domain;
using Ledgerline.Fraud.Endpoints;
using Ledgerline.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddLedgerlineService(FraudService.Name, typeof(FraudService).Assembly, FraudStore.Configure);
builder.AddAccountDirectorySubscription(FraudService.Name);
builder.Services.AddHostedService<BlocklistSeeder>();
builder.Services.AddOptions<FraudPolicy>().Bind(builder.Configuration.GetSection("Fraud:Policy"));

var app = builder.Build();
app.UseLedgerlineService();
ReviewEndpoints.Map(app.MapGroup("/api/fraud"));

await app.RunAsync();
