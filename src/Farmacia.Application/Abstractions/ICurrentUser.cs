namespace Farmacia.Application.Abstractions;

/// <summary>
/// The user making the current request. Like the pharmacy, it never comes from the request body:
/// the API reads it from the signed JWT ("sub" and "role" claims).
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }

    bool IsInRole(string role);
}
