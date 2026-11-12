using Farmacia.Application.Abstractions;
using Farmacia.Application.Authorization;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Application.Tests.Fakes;
using Farmacia.Application.Users;

namespace Farmacia.Application.Tests.Users;

public class UserUseCasesTests
{
    private static readonly Guid PharmacyId = Guid.CreateVersion7();
    private static readonly Guid OtherPharmacyId = Guid.CreateVersion7();

    private readonly InMemoryUserAccounts _users = new();
    private readonly UserAccountDto _me;

    public UserUseCasesTests() => _me = _users.Add(PharmacyId, Roles.Owner);

    private FakeCurrentPharmacy Pharmacy => new(PharmacyId);
    private FakeCurrentUser Me => new(_me.Id, Roles.Owner);

    [Fact]
    public async Task Create_InTheOwnersPharmacy_WithAPharmacyRole()
    {
        var id = await new CreateUserHandler(_users, Pharmacy).Handle(
            new CreateUserCommand(" Carlos Ruiz ", " carlos@drogueria.co ", Roles.Cashier, "Temporal2026"), CancellationToken.None);

        var created = _users.Get(id);
        Assert.Equal((PharmacyId, "Carlos Ruiz", "carlos@drogueria.co", Roles.Cashier), (created.PharmacyId, created.User.FullName, created.User.Email, created.User.Role));
    }

    [Theory]
    [InlineData(Roles.PlatformAdmin)] // an owner must never create a platform administrator
    [InlineData("Admin")]
    public async Task Create_WithARoleThatIsNotOfAPharmacy_IsInvalid(string role)
    {
        await Assert.ThrowsAsync<ValidationException>(() => new CreateUserHandler(_users, Pharmacy).Handle(
            new CreateUserCommand("Carlos", "carlos@drogueria.co", role, "Temporal2026"), CancellationToken.None));
    }

    [Fact]
    public async Task AUserOfAnotherPharmacy_IsNotFound_ForEveryAction()
    {
        var stranger = _users.Add(OtherPharmacyId, Roles.Cashier);

        await Assert.ThrowsAsync<NotFoundException>(() => Deactivate(stranger.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => ChangeRole(stranger.Id, Roles.StockKeeper));
        await Assert.ThrowsAsync<NotFoundException>(() => ResetPassword(stranger.Id));
        Assert.True(_users.Get(stranger.Id).User.IsActive);
    }

    [Fact]
    public async Task TheOwner_CannotDeactivateOrDemoteOrResetThemselves()
    {
        _users.Add(PharmacyId, Roles.Owner); // even with another owner

        await Assert.ThrowsAsync<ValidationException>(() => Deactivate(_me.Id));
        await Assert.ThrowsAsync<ValidationException>(() => ChangeRole(_me.Id, Roles.Cashier));
        await Assert.ThrowsAsync<ValidationException>(() => ResetPassword(_me.Id));
    }

    [Fact]
    public async Task ThePharmacy_NeverLosesItsLastActiveOwner()
    {
        // The other owner is the last active one: a just-deactivated owner (_me) still has a valid token for minutes.
        var lastOwner = _users.Add(PharmacyId, Roles.Owner);
        _users.SetActive(_me.Id, false);

        await Assert.ThrowsAsync<ValidationException>(() => Deactivate(lastOwner.Id));
        await Assert.ThrowsAsync<ValidationException>(() => ChangeRole(lastOwner.Id, Roles.Cashier));
        Assert.Equal(Roles.Owner, _users.Get(lastOwner.Id).User.Role);
    }

    [Fact]
    public async Task WithAnotherActiveOwner_AnOwnerCanBeDeactivatedOrDemoted()
    {
        var partner = _users.Add(PharmacyId, Roles.Owner);

        await ChangeRole(partner.Id, Roles.StockKeeper);
        await Deactivate(partner.Id);

        Assert.Equal((Roles.StockKeeper, false), (_users.Get(partner.Id).User.Role, _users.Get(partner.Id).User.IsActive));
    }

    private Task Deactivate(Guid userId) =>
        new SetUserActiveHandler(_users, Pharmacy, Me).Handle(new SetUserActiveCommand(userId, false), CancellationToken.None);

    private Task ChangeRole(Guid userId, string role) =>
        new ChangeUserRoleHandler(_users, Pharmacy, Me).Handle(new ChangeUserRoleCommand(userId, role), CancellationToken.None);

    private Task ResetPassword(Guid userId) =>
        new ResetUserPasswordHandler(_users, Pharmacy, Me).Handle(new ResetUserPasswordCommand(userId, "Temporal2026"), CancellationToken.None);

    private sealed class InMemoryUserAccounts : IUserAccounts
    {
        private readonly Dictionary<Guid, (Guid PharmacyId, UserAccountDto User)> _users = [];

        public UserAccountDto Add(Guid pharmacyId, string role)
        {
            var user = new UserAccountDto(Guid.CreateVersion7(), $"Usuario {role}", $"{Guid.NewGuid():N}@x.co", role, true, false);
            _users[user.Id] = (pharmacyId, user);
            return user;
        }

        public (Guid PharmacyId, UserAccountDto User) Get(Guid id) => _users[id];

        public void SetActive(Guid id, bool active) => _users[id] = (_users[id].PharmacyId, _users[id].User with { IsActive = active });

        public Task<IReadOnlyList<UserAccountDto>> ListAsync(Guid pharmacyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UserAccountDto>>(_users.Values.Where(u => u.PharmacyId == pharmacyId).Select(u => u.User).ToList());

        public Task<UserAccountDto?> GetAsync(Guid pharmacyId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(_users.TryGetValue(userId, out var u) && u.PharmacyId == pharmacyId ? u.User : null);

        public Task<int> CountActiveOwnersAsync(Guid pharmacyId, CancellationToken cancellationToken) =>
            Task.FromResult(_users.Values.Count(u => u.PharmacyId == pharmacyId && u.User is { IsActive: true, Role: Roles.Owner }));

        public Task<Guid> CreateAsync(Guid pharmacyId, NewUserAccount user, CancellationToken cancellationToken)
        {
            var created = new UserAccountDto(Guid.CreateVersion7(), user.FullName, user.Email, user.Role, true, true);
            _users[created.Id] = (pharmacyId, created);
            return Task.FromResult(created.Id);
        }

        public Task SetRoleAsync(Guid pharmacyId, Guid userId, string role, CancellationToken cancellationToken)
        {
            _users[userId] = (pharmacyId, _users[userId].User with { Role = role });
            return Task.CompletedTask;
        }

        public Task SetActiveAsync(Guid pharmacyId, Guid userId, bool isActive, CancellationToken cancellationToken)
        {
            SetActive(userId, isActive);
            return Task.CompletedTask;
        }

        public Task ResetPasswordAsync(Guid pharmacyId, Guid userId, string temporaryPassword, CancellationToken cancellationToken)
        {
            _users[userId] = (pharmacyId, _users[userId].User with { MustChangePassword = true });
            return Task.CompletedTask;
        }
    }
}
