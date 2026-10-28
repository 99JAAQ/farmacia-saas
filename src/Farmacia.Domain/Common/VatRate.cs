namespace Farmacia.Domain.Common;

/// <summary>Colombian VAT (IVA) rates. The value is the percentage.</summary>
public enum VatRate
{
    /// <summary>0%: excluded or exempt goods (most medicines).</summary>
    Exempt = 0,

    /// <summary>5%.</summary>
    Reduced = 5,

    /// <summary>19%: general rate (cosmetics, personal care, ...).</summary>
    Standard = 19,
}

public static class VatRateExtensions
{
    public static decimal AsFraction(this VatRate rate) => (int)rate / 100m;

    /// <summary>
    /// The VAT contained in an amount that already includes it (retail prices in Colombia do):
    /// 12.000 at 19% contains 12.000 × 19 / 119 = 1.915,97.
    /// </summary>
    public static decimal IncludedIn(this VatRate rate, decimal amountWithVat) =>
        Money.Round(amountWithVat * (int)rate / (100 + (int)rate));
}
