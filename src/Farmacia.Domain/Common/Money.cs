using System.Globalization;

namespace Farmacia.Domain.Common;

internal static class Money
{
    /// <summary>
    /// Rounds an amount to cents. Always away from zero, the way people round money by hand
    /// (the .NET default, banker's rounding, would turn 0.125 into 0.12).
    /// </summary>
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Colombian format for messages, e.g. "$87.000". Built by hand instead of with the es-CO culture so the
    /// result does not depend on the culture data installed on the server (some Docker images have none).
    /// </summary>
    public static string Format(decimal amount) =>
        (amount < 0 ? "-$" : "$")
        + Math.Abs(amount).ToString("#,0.##", CultureInfo.InvariantCulture).Replace(',', '_').Replace('.', ',').Replace('_', '.');
}
