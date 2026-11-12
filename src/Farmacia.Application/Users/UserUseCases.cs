using Farmacia.Application.Abstractions;
using Farmacia.Application.Common.Exceptions;
using MediatR;

namespace Farmacia.Application.Users;

// The owner manages the pharmacy's users. Small use cases, one per action, with the rules in UserRules.

/// <summary>Every user of the pharmacy, active or not.</summary>
public sealed record ListUsersQuery : IRequest<IReadOnlyList<UserAccountDto>>;

public sealed class ListUsersHandler(IUserAccounts users, ICurrentPharmacy currentPharmacy)
    : IRequestHandler<ListUsersQuery, IReadOnlyList<UserAccountDto>>
{
    public Task<IReadOnlyList<UserAccountDto>> Handle(ListUsersQuery query, CancellationToken cancellationToken) =>
        users.ListAsync(currentPharmacy.PharmacyId, cancellationToken);
}

/// <summary>A new user of the pharmacy, with a temporary password the owner tells them.</summary>
public sealed record CreateUserCommand(string FullName, string Email, string Role, string TemporaryPassword) : IRequest<Guid>;

public sealed class CreateUserHandler(IUserAccounts users, ICurrentPharmacy currentPharmacy)
    : IRequestHandler<CreateUserCommand, Guid>
{
    public Task<Guid> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.FullName))
            throw new ValidationException("Escriba el nombre del usuario.");
        if (string.IsNullOrWhiteSpace(command.Email))
            throw new ValidationException("Escriba el email del usuario: con él inicia sesión.");
        if (string.IsNullOrEmpty(command.TemporaryPassword))
            throw new ValidationException("Escriba una contraseña temporal.");
        UserRules.EnsurePharmacyRole(command.Role);

        return users.CreateAsync(
            currentPharmacy.PharmacyId,
            new NewUserAccount(command.FullName.Trim(), command.Email.Trim(), command.Role, command.TemporaryPassword),
            cancellationToken);
    }
}

public sealed record ChangeUserRoleCommand(Guid UserId, string Role) : IRequest;

public sealed class ChangeUserRoleHandler(IUserAccounts users, ICurrentPharmacy currentPharmacy, ICurrentUser currentUser)
    : IRequestHandler<ChangeUserRoleCommand>
{
    public async Task Handle(ChangeUserRoleCommand command, CancellationToken cancellationToken)
    {
        UserRules.EnsurePharmacyRole(command.Role);
        var pharmacyId = currentPharmacy.PharmacyId;
        var user = await UserRules.GetAsync(users, pharmacyId, command.UserId, cancellationToken);
        if (user.Role == command.Role)
            return;

        UserRules.EnsureNotSelf(user.Id, currentUser, "No puede cambiar su propio rol: pídaselo a otro dueño.");
        await UserRules.EnsureAnotherActiveOwnerAsync(users, pharmacyId, user, cancellationToken);

        await users.SetRoleAsync(pharmacyId, user.Id, command.Role, cancellationToken);
    }
}

/// <summary>Deactivate (someone who left; never deleted: sales and the kardex point to them) or reactivate.</summary>
public sealed record SetUserActiveCommand(Guid UserId, bool IsActive) : IRequest;

public sealed class SetUserActiveHandler(IUserAccounts users, ICurrentPharmacy currentPharmacy, ICurrentUser currentUser)
    : IRequestHandler<SetUserActiveCommand>
{
    public async Task Handle(SetUserActiveCommand command, CancellationToken cancellationToken)
    {
        var pharmacyId = currentPharmacy.PharmacyId;
        var user = await UserRules.GetAsync(users, pharmacyId, command.UserId, cancellationToken);
        if (user.IsActive == command.IsActive)
            return;

        if (!command.IsActive)
        {
            UserRules.EnsureNotSelf(user.Id, currentUser, "No puede desactivarse a sí mismo: pídaselo a otro dueño.");
            await UserRules.EnsureAnotherActiveOwnerAsync(users, pharmacyId, user, cancellationToken);
        }

        await users.SetActiveAsync(pharmacyId, user.Id, command.IsActive, cancellationToken);
    }
}

/// <summary>For a user who forgot their password: the owner sets a temporary one, which must be changed at login.</summary>
public sealed record ResetUserPasswordCommand(Guid UserId, string TemporaryPassword) : IRequest;

public sealed class ResetUserPasswordHandler(IUserAccounts users, ICurrentPharmacy currentPharmacy, ICurrentUser currentUser)
    : IRequestHandler<ResetUserPasswordCommand>
{
    public async Task Handle(ResetUserPasswordCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(command.TemporaryPassword))
            throw new ValidationException("Escriba una contraseña temporal.");

        var pharmacyId = currentPharmacy.PharmacyId;
        var user = await UserRules.GetAsync(users, pharmacyId, command.UserId, cancellationToken);
        UserRules.EnsureNotSelf(user.Id, currentUser, "Para su propia contraseña use «Cambiar contraseña».");

        await users.ResetPasswordAsync(pharmacyId, user.Id, command.TemporaryPassword, cancellationToken);
    }
}
