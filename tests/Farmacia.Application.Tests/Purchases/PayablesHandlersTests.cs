using Farmacia.Application.Common.Exceptions;
using Farmacia.Application.Purchases;
using Farmacia.Application.Purchases.ListPayables;
using Farmacia.Application.Purchases.RegisterSupplierPayment;
using Farmacia.Application.Tests.Fakes;
using Farmacia.Domain.Common;
using Farmacia.Domain.Purchases;

namespace Farmacia.Application.Tests.Purchases;

public class PayablesHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 10, 0, 0, TimeSpan.FromHours(-5));

    private readonly Guid _pharmacyId = Guid.CreateVersion7();
    private readonly Guid _owner = Guid.CreateVersion7();
    private readonly InMemoryPurchaseRepository _purchases = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    /// <summary>A confirmed invoice of 90.000; credit ones fall due on the given date.</summary>
    private Purchase Invoice(string number, DateOnly? due, PaymentTerm term = PaymentTerm.Credit)
    {
        var purchase = Purchase.CreateDraft(_pharmacyId, Guid.CreateVersion7(), number, new DateOnly(2026, 9, 1), term, due);
        purchase.AddItem(Guid.CreateVersion7(), Guid.CreateVersion7(), 30, 10, 0, 9_000m, 0m, VatRate.Exempt, "L1", new DateOnly(2028, 1, 31));
        purchase.Confirm(90_000m, vatIsPartOfCost: false, new DateTimeOffset(2026, 9, 2, 9, 0, 0, TimeSpan.FromHours(-5)));
        _purchases.Add(purchase);
        return purchase;
    }

    private PurchaseReader Reader() => new(new InMemoryProductRepository(), new InMemorySupplierRepository(), new FakeUserReader(), new FakeClock(Now));

    [Fact]
    public async Task Payables_AreTheCreditInvoicesStillOwed_TheMostOverdueFirst()
    {
        Invoice("FE-LATER", new DateOnly(2026, 12, 1));         // due in 23 days
        Invoice("FE-OLD", new DateOnly(2026, 10, 1));           // 38 days overdue
        Invoice("FE-RECENT", new DateOnly(2026, 11, 1));        // 7 days overdue
        Invoice("FE-CASH", null, PaymentTerm.Cash);             // paid on receipt
        Invoice("FE-PAID", new DateOnly(2026, 10, 15)).RegisterPayment(90_000m, SupplierPaymentMethod.Cash, null, Now, _owner);

        var payables = await new ListPayablesHandler(_purchases, new FakeCurrentPharmacy(_pharmacyId), Reader())
            .Handle(new ListPayablesQuery(null), CancellationToken.None);

        Assert.Equal(
            [("FE-OLD", 38, -38), ("FE-RECENT", 7, -7), ("FE-LATER", 0, 23)],
            payables.Select(p => (p.InvoiceNumber, p.DaysOverdue, p.DaysToDue!.Value)));
        Assert.All(payables, p => Assert.Equal(90_000m, p.Balance));
    }

    [Fact]
    public async Task RegisterPayment_RecordsWhoPaid_AndAnotherPharmacysInvoice_IsNotFound()
    {
        var invoice = Invoice("FE-1", new DateOnly(2026, 12, 1));
        RegisterSupplierPaymentHandler Handler(Guid pharmacyId) =>
            new(_purchases, _unitOfWork, new FakeCurrentPharmacy(pharmacyId), new FakeCurrentUser(_owner), new FakeClock(Now));

        await Handler(_pharmacyId).Handle(new RegisterSupplierPaymentCommand(invoice.Id, 30_000m, SupplierPaymentMethod.Check, "CHQ-0042"), CancellationToken.None);

        var payment = Assert.Single(invoice.Payments);
        Assert.Equal((_owner, Now, "CHQ-0042"), (payment.PaidBy, payment.PaidAt, payment.Reference));
        Assert.Equal(60_000m, invoice.Balance);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(Guid.CreateVersion7()).Handle(new RegisterSupplierPaymentCommand(invoice.Id, 1_000m, SupplierPaymentMethod.Cash, null), CancellationToken.None));
    }
}
