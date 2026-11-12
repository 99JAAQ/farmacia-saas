using Farmacia.Api.Authentication;
using Farmacia.Application.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Farmacia.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = Policies.ViewDashboard)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class DashboardController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Today, in Colombia: sales, returns, VAT, cost and gross margin, by payment method, by hour and the best sellers.
    /// The screen refreshes it on every "sales" notice (real time).
    /// </summary>
    [HttpGet("today")]
    [ProducesResponseType<DailySummaryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DailySummaryDto>> Today(CancellationToken cancellationToken) =>
        await sender.Send(new GetDailySummaryQuery(), cancellationToken);
}
