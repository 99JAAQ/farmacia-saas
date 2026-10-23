using Farmacia.Application.Abstractions;
using Farmacia.Application.Authorization;
using Farmacia.Application.Common.Exceptions;

namespace Farmacia.Api.Authentication;

/// <summary>
/// The tenant comes from the signed JWT: the client cannot change it without invalidating the signature.
/// </summary>
internal sealed class HttpContextCurrentPharmacy(IHttpContextAccessor httpContextAccessor) : ICurrentPharmacy
{
    public Guid PharmacyId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst(ClaimNames.PharmacyId)?.Value, out var pharmacyId)
            ? pharmacyId
            : throw new UnauthorizedException("La sesión no indica la farmacia. Inicie sesión de nuevo.");
}
