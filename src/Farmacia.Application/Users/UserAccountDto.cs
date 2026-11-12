namespace Farmacia.Application.Users;

/// <param name="Role">A pharmacy user has one role: Owner, Cashier or StockKeeper.</param>
/// <param name="MustChangePassword">True while the user still has the temporary password the owner gave them.</param>
public sealed record UserAccountDto(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    bool IsActive,
    bool MustChangePassword);
