using System.Globalization;

namespace Ledgerline.Ledger.Domain;

/// <summary>
/// An amount in minor units (cents). Integers make every posting exact: no binary rounding, and the "sum of an entry
/// is zero" invariant is an integer equality. The API speaks decimal euros; the conversion lives at the edge.
/// </summary>
public readonly record struct Money(long Cents) : IComparable<Money>
{
    public static readonly Money Zero = new(0);

    public const string Currency = "EUR";

    public bool IsPositive => Cents > 0;

    public static Money FromEuros(decimal euros)
    {
        var cents = euros * 100m;
        if (cents != decimal.Truncate(cents))
        {
            throw new ArgumentException("Amounts have at most two decimal places.", nameof(euros));
        }

        return new Money(checked((long)cents));
    }

    public decimal ToEuros() => Cents / 100m;

    public static Money operator +(Money left, Money right) => new(checked(left.Cents + right.Cents));

    public static Money operator -(Money left, Money right) => new(checked(left.Cents - right.Cents));

    public static Money operator -(Money value) => new(checked(-value.Cents));

    public static bool operator <(Money left, Money right) => left.Cents < right.Cents;

    public static bool operator >(Money left, Money right) => left.Cents > right.Cents;

    public static bool operator <=(Money left, Money right) => left.Cents <= right.Cents;

    public static bool operator >=(Money left, Money right) => left.Cents >= right.Cents;

    public int CompareTo(Money other) => Cents.CompareTo(other.Cents);

    public override string ToString() => ToEuros().ToString("0.00", CultureInfo.InvariantCulture) + " " + Currency;
}
