namespace Farmacia.Application.Platform;

/// <summary>A pharmacy as the platform administrator sees it: who it is and who runs it, never its business data.</summary>
public sealed record PlatformPharmacyDto(
    Guid Id,
    string Name,
    string TaxId,
    string? Address,
    string? Phone,
    bool IsVatResponsible,
    IReadOnlyList<PlatformOwnerDto> Owners,
    int UserCount);

public sealed record PlatformOwnerDto(string FullName, string Email);
