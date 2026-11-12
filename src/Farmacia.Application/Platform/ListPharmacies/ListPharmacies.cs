using Farmacia.Application.Abstractions;
using MediatR;

namespace Farmacia.Application.Platform.ListPharmacies;

/// <summary>Every pharmacy on the platform, by name.</summary>
public sealed record ListPharmaciesQuery : IRequest<IReadOnlyList<PlatformPharmacyDto>>;

public sealed class ListPharmaciesHandler(IPlatformPharmacies platform)
    : IRequestHandler<ListPharmaciesQuery, IReadOnlyList<PlatformPharmacyDto>>
{
    public Task<IReadOnlyList<PlatformPharmacyDto>> Handle(ListPharmaciesQuery query, CancellationToken cancellationToken) =>
        platform.ListAsync(cancellationToken);
}
