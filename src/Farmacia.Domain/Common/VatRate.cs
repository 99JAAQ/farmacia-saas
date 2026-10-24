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
}
