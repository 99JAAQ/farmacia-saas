namespace Farmacia.Api.Controllers;

/// <summary>Body of every 201 response: the Id of what was created (its URL goes in the Location header).</summary>
public sealed record CreatedResponse(Guid Id);
