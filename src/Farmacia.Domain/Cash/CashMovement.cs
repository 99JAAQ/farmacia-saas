using Farmacia.Domain.Common;

namespace Farmacia.Domain.Cash;

/// <summary>Cash in or out of the drawer that is not a sale. Created only through <see cref="CashSession"/>.</summary>
public class CashMovement : TenantEntity
{
    private CashMovement() { } // EF Core

    internal CashMovement(
        Guid pharmacyId, Guid cashSessionId, CashMovementType type, decimal amount, string reason,
        Guid registeredBy, DateTimeOffset occurredAt)
        : base(pharmacyId)
    {
        if (!Enum.IsDefined(type))
            throw new DomainException("El tipo de movimiento no es válido.");

        if (amount <= 0)
            throw new DomainException("El valor del movimiento debe ser mayor que cero.");

        if (registeredBy == Guid.Empty)
            throw new DomainException("El usuario que registra el movimiento es obligatorio.");

        CashSessionId = cashSessionId;
        Type = type;
        Amount = amount;
        Reason = Guard.Required(reason, "El motivo del movimiento");
        RegisteredBy = registeredBy;
        OccurredAt = occurredAt;
    }

    public Guid CashSessionId { get; private init; }
    public CashMovementType Type { get; private init; }

    /// <summary>Always positive; <see cref="Type"/> says whether it comes in or goes out.</summary>
    public decimal Amount { get; private init; }

    public string Reason { get; private init; } = null!;
    public Guid RegisteredBy { get; private init; }
    public DateTimeOffset OccurredAt { get; private init; }

    /// <summary>The amount with its sign, as it changes the cash in the drawer.</summary>
    public decimal SignedAmount => Type == CashMovementType.Income ? Amount : -Amount;
}
