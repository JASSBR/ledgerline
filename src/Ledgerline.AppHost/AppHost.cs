// `dotnet run --project src/Ledgerline.AppHost` starts the whole bank: PostgreSQL (one database per service), RabbitMQ,
// Keycloak with the demo realm, the three services, the YARP gateway and the Angular app — wired by service discovery,
// with distributed traces across HTTP and RabbitMQ in the Aspire dashboard.
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").WithDataVolume().WithLifetime(ContainerLifetime.Persistent);
var ledgerDb = postgres.AddDatabase("ledgerdb");
var paymentsDb = postgres.AddDatabase("paymentsdb");
var fraudDb = postgres.AddDatabase("frauddb");

var messaging = builder.AddRabbitMQ("messaging").WithManagementPlugin().WithLifetime(ContainerLifetime.Persistent);

// Keycloak serves HTTPS with the Aspire dev certificate (also on the "http" endpoint name).
// Realm re-imported on each start (no data volume): the demo users and their ids are always the documented ones.
var keycloak = builder.AddKeycloak("keycloak", port: 8080).WithRealmImport("../../deploy/keycloak");
var authority = ReferenceExpression.Create($"{keycloak.GetEndpoint("http")}/realms/ledgerline");

IResourceBuilder<ProjectResource> Service<TProject>(string name, IResourceBuilder<PostgresDatabaseResource> database)
    where TProject : IProjectMetadata, new() =>
    builder.AddProject<TProject>(name)
        .WithReference(database).WaitFor(database)
        .WithReference(messaging).WaitFor(messaging)
        .WaitFor(keycloak)
        .WithEnvironment("Auth__Authority", authority)
        .WithHttpHealthCheck("/health");

var ledger = Service<Projects.Ledgerline_Ledger>("ledger", ledgerDb).WithEnvironment("Demo__Seed", "true");
var payments = Service<Projects.Ledgerline_Payments>("payments", paymentsDb);
var fraud = Service<Projects.Ledgerline_Fraud>("fraud", fraudDb);

var gateway = builder.AddProject<Projects.Ledgerline_Gateway>("gateway")
    .WithReference(ledger).WithReference(payments).WithReference(fraud)
    .WithExternalHttpEndpoints();

builder.AddJavaScriptApp("web", "../../web", "start")
    .WithHttpEndpoint(port: 4200, env: "PORT")
    .WithReference(gateway)
    .WaitFor(gateway);

await builder.Build().RunAsync();
