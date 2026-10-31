using Farmacia.Application.Abstractions.Persistence;
using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Credit;

namespace Farmacia.Application.Credit;

internal static class CreditAccountRepositoryExtensions
{
    /// <summary>A customer of another pharmacy has no account here either: 404, never 403.</summary>
    public static async Task<CreditAccount> GetOrThrowAsync(
        this ICreditAccountRepository accounts, Guid pharmacyId, Guid customerId, CancellationToken cancellationToken) =>
        await accounts.GetByCustomerAsync(pharmacyId, customerId, cancellationToken)
            ?? throw new NotFoundException("El cliente no tiene fiado habilitado.");
}
