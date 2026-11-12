using Farmacia.Api.Authentication;
using Farmacia.Application.Platform;
using Farmacia.Application.Platform.CreatePharmacy;
using Farmacia.Application.Platform.ListPharmacies;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Farmacia.Api.Controllers;

/// <summary>
/// The platform administrator's endpoints: the pharmacies (tenants) and their owners. Nothing here shows a
/// pharmacy's sales, stock or customers.
/// </summary>
[ApiController]
[Route("api/platform/pharmacies")]
[Authorize(Policy = Policies.ManagePlatform)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class PlatformController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PlatformPharmacyDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListPharmaciesQuery(), cancellationToken));

    /// <summary>A new pharmacy with its owner, in one transaction. The owner then creates the other users.</summary>
    [HttpPost]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreatePharmacyCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return Created($"/api/platform/pharmacies/{id}", new CreatedResponse(id));
    }
}
