using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Ledgerline.IntegrationTests.Support;

/// <summary>Boots one real service (Marten, Wolverine, RabbitMQ) on a fresh database, with test authentication.</summary>
public class ServiceFactory<TEntryPoint>(
    string connectionStringName,
    string databaseConnectionString,
    string rabbitConnectionString,
    bool seed,
    IReadOnlyDictionary<string, string>? settings = null)
    : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{connectionStringName}", databaseConnectionString);
        builder.UseSetting("ConnectionStrings:messaging", rabbitConnectionString);
        builder.UseSetting("Demo:Seed", seed ? "true" : "false");
        builder.UseSetting("Auth:Authority", "https://keycloak.invalid/realms/test");
        foreach (var (key, value) in settings ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            });
        });
    }
}
