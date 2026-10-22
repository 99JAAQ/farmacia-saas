namespace Farmacia.Domain.Common;

internal static class Guard
{
    public static string Required(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{fieldName} es obligatorio.");

        return value.Trim();
    }

    public static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string? OptionalEmail(string? value)
    {
        var email = Optional(value);

        if (email is not null && !System.Net.Mail.MailAddress.TryCreate(email, out _))
            throw new DomainException($"El correo '{email}' no es válido.");

        return email;
    }
}
