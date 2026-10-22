using Farmacia.Application.Common.Exceptions;
using Farmacia.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace Farmacia.Api.ErrorHandling;

/// <summary>
/// Turns known exceptions into ProblemDetails (RFC 9457) responses with a user-facing message.
/// Anything else falls through to the default handler: 500 without internal details.
/// </summary>
internal sealed class ProblemDetailsExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int Status, string Title)? mapping = exception switch
        {
            DomainException or ValidationException => (StatusCodes.Status400BadRequest, "Solicitud inválida"),
            NotFoundException => (StatusCodes.Status404NotFound, "No encontrado"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflicto"),
            _ => null,
        };

        if (mapping is not { } map)
            return false;

        httpContext.Response.StatusCode = map.Status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = map.Status, Title = map.Title, Detail = exception.Message },
        });
    }
}
