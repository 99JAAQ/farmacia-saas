namespace Farmacia.Domain.Common;

/// <summary>
/// Colombian tax id (NIT) with its verification digit, validated with the DIAN modulo 11 algorithm.
/// Value object: if an instance exists, it is valid.
/// </summary>
public sealed record Nit
{
    // DIAN weights, applied to the digits from right to left.
    private static readonly int[] Weights = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71];

    private Nit(string number, int verificationDigit)
    {
        Number = number;
        VerificationDigit = verificationDigit;
    }

    public string Number { get; }
    public int VerificationDigit { get; }

    /// <summary>Normalized form, e.g. "900123456-8".</summary>
    public string Value => $"{Number}-{VerificationDigit}";

    /// <summary>Accepts "900123456-8", "900.123.456-8" or "900 123 456 - 8". The dash is required.</summary>
    public static Nit Parse(string? value)
    {
        var raw = Guard.Required(value, "El NIT");
        var parts = raw.Replace(".", "").Replace(" ", "").Split('-');

        if (parts.Length != 2
            || parts[0].Length is 0 || parts[0].Length > Weights.Length || !parts[0].All(char.IsAsciiDigit)
            || parts[1].Length != 1 || !char.IsAsciiDigit(parts[1][0]))
        {
            throw new DomainException($"El NIT '{raw}' debe tener el formato 900123456-8.");
        }

        var number = parts[0];
        var verificationDigit = parts[1][0] - '0';

        if (CalculateVerificationDigit(number) != verificationDigit)
            throw new DomainException($"El dígito de verificación del NIT '{raw}' no es válido.");

        return new Nit(number, verificationDigit);
    }

    /// <summary>Builds a NIT from the number alone, calculating its verification digit ("900123456" -> "900123456-8").</summary>
    public static Nit FromNumber(string? number)
    {
        var digits = Guard.Required(number, "El NIT").Replace(".", "").Replace(" ", "");

        if (digits.Length > Weights.Length || !digits.All(char.IsAsciiDigit))
            throw new DomainException($"El NIT '{number}' solo puede tener números (sin el dígito de verificación).");

        return new Nit(digits, CalculateVerificationDigit(digits));
    }

    private static int CalculateVerificationDigit(string number)
    {
        var sum = 0;
        for (var i = 0; i < number.Length; i++)
            sum += (number[number.Length - 1 - i] - '0') * Weights[i];

        var remainder = sum % 11;
        return remainder > 1 ? 11 - remainder : remainder;
    }

    public override string ToString() => Value;
}
