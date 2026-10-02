namespace Ledgerline.Ledger;

/// <summary>
/// Names this service (database schema, queue prefix) and anchors its assembly for integration tests:
/// every service has its own generated Program class, which would be ambiguous from a shared test project.
/// </summary>
public sealed class LedgerService
{
    public const string Name = "ledger";

    private LedgerService()
    {
    }
}
