using System.Runtime.CompilerServices;
using JasperFx.CommandLine;

namespace Ledgerline.IntegrationTests.Support;

internal static class JasperFxSetup
{
    /// <summary>
    /// The services end with RunJasperFxCommands (for `codegen write`). Under WebApplicationFactory no command runs,
    /// so the host must be told to start by itself, or every factory waits for a host that never starts.
    /// </summary>
    [ModuleInitializer]
    internal static void StartHostsUnderTest() => JasperFxEnvironment.AutoStartHost = true;
}
