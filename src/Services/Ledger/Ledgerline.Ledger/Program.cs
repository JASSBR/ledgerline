using Ledgerline.Hosting;
using Ledgerline.Ledger;
using Ledgerline.Ledger.Endpoints;
using Ledgerline.Ledger.Persistence;
using Ledgerline.Ledger.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.AddLedgerlineService(LedgerService.Name, typeof(LedgerService).Assembly, LedgerStore.Configure);
if (builder.Configuration.GetValue<bool>("Demo:Seed"))
{
    builder.Services.AddHostedService<DemoBankSeeder>();
}

var app = builder.Build();
app.UseLedgerlineService();

var api = app.MapGroup("/api/ledger");
AccountEndpoints.Map(api);
OperationsEndpoints.Map(api);

await app.RunAsync();

