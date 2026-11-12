using Farmacia.Api.Authentication;
using Farmacia.Application.Pharmacies;
using Farmacia.Application.Pharmacies.GetCurrentPharmacy;
using Farmacia.Application.Pharmacies.UpdatePharmacy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Farmacia.Api.Controllers;

/// <summary>
/// The signed-in user's own pharmacy, taken from the token: there is no Id in the URL, so there is no way to ask for
/// another pharmacy's data.
/// </summary>
[ApiController]
[Route("api/pharmacy")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class PharmacyController(ISender sender) : ControllerBase
{
    /// <summary>Any user of the pharmacy: the cashier prints its name and NIT on every ticket.</summary>
    [HttpGet]
    [ProducesResponseType<PharmacyDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PharmacyDto>> Get(CancellationToken cancellationToken) =>
        await sender.Send(new GetCurrentPharmacyQuery(), cancellationToken);

    /// <summary>The owner updates the name, address, phone and VAT setting. The NIT does not change.</summary>
    [HttpPut]
    [Authorize(Policy = Policies.ManagePharmacy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(UpdatePharmacyCommand command, CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return NoContent();
    }
}
