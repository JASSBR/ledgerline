namespace Ledgerline.Ledger;

/// <summary>
/// Fixed identities of the demo bank. The owner ids are the Keycloak user ids of the realm export
/// (deploy/keycloak/ledgerline-realm.json), so a logged-in demo user finds "their" accounts.
/// </summary>
public static class DemoAccounts
{
    public static readonly Guid Treasury = Guid.Parse("00000000-0000-7000-8000-000000000001");

    public const string AliceUserId = "a11ce000-0000-4000-8000-000000000001";
    public const string BobUserId = "b0b00000-0000-4000-8000-000000000002";
    public const string ChloeUserId = "c4104e00-0000-4000-8000-000000000003";

    public static readonly Guid AliceCurrent = Guid.Parse("01920000-0000-7000-8000-00000000a001");
    public static readonly Guid AliceSavings = Guid.Parse("01920000-0000-7000-8000-00000000a002");
    public static readonly Guid BobCurrent = Guid.Parse("01920000-0000-7000-8000-00000000b001");
    public static readonly Guid ChloeCurrent = Guid.Parse("01920000-0000-7000-8000-00000000c001");

    /// <summary>A customer account whose IBAN is on Fraud's blocklist (FR76 9999 9000 0166 6666 6666 610).</summary>
    public static readonly Guid MuleAccount = Guid.Parse("01920000-0000-7000-8000-00000000d001");

    public const long AliceCurrentNumber = 10_000_000_001;
    public const long AliceSavingsNumber = 10_000_000_002;
    public const long BobCurrentNumber = 20_000_000_001;
    public const long ChloeCurrentNumber = 30_000_000_001;
    public const long MuleAccountNumber = 66_666_666_666;
}
