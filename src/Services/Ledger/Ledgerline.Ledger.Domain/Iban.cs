using System.Globalization;
using System.Numerics;
using System.Text;

namespace Ledgerline.Ledger.Domain;

/// <summary>
/// International Bank Account Number, validated with the ISO 13616 mod-97 checksum.
/// Demo accounts use the bank code 99999, which no real French bank holds.
/// </summary>
public readonly record struct Iban
{
    private const string DemoBankCode = "99999";
    private const string DemoBranchCode = "00001";

    private Iban(string value) => Value = value;

    public string Value { get; }

    public static bool TryParse(string? input, out Iban iban)
    {
        iban = default;
        var compact = new string((input ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        if (compact.Length is < 15 or > 34 || !compact.All(char.IsAsciiLetterOrDigit) || !char.IsAsciiLetter(compact[0]) || !char.IsAsciiLetter(compact[1]))
        {
            return false;
        }

        if (Mod97(compact[4..] + compact[..4]) != 1)
        {
            return false;
        }

        iban = new Iban(compact);
        return true;
    }

    /// <summary>A French-format IBAN (FR76…) for an internal account number, with a valid national RIB key and checksum.</summary>
    public static Iban ForAccountNumber(long accountNumber)
    {
        var account = accountNumber.ToString("D11", CultureInfo.InvariantCulture);
        var ribKey = 97 - (int)(BigInteger.Parse(DemoBankCode + DemoBranchCode + account + "00", CultureInfo.InvariantCulture) % 97);
        var bban = $"{DemoBankCode}{DemoBranchCode}{account}{ribKey:D2}";
        var check = 98 - Mod97(bban + "FR00");
        return new Iban($"FR{check:D2}{bban}");
    }

    /// <summary>Grouped by four, as printed on a bank statement.</summary>
    public string Formatted() => string.Join(' ', Value.Chunk(4).Select(group => new string(group)));

    public override string ToString() => Value;

    private static int Mod97(string rearranged)
    {
        var digits = new StringBuilder(rearranged.Length * 2);
        foreach (var c in rearranged)
        {
            digits.Append(char.IsAsciiLetter(c) ? (c - 'A' + 10).ToString(CultureInfo.InvariantCulture) : c.ToString());
        }

        return (int)(BigInteger.Parse(digits.ToString(), CultureInfo.InvariantCulture) % 97);
    }
}
