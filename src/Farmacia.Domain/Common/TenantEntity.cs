namespace Farmacia.Domain.Common;

public abstract class TenantEntity : Entity
{
    protected TenantEntity() { } // EF Core

    protected TenantEntity(Guid pharmacyId)
    {
        if (pharmacyId == Guid.Empty)
            throw new DomainException("La farmacia es obligatoria.");

        PharmacyId = pharmacyId;
    }

    public Guid PharmacyId { get; private init; }
}
