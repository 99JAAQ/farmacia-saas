using Farmacia.Application.Credit;
using Farmacia.Domain.Credit;
using Farmacia.Domain.Customers;

namespace Farmacia.Application.Customers;

/// <param name="Credit">Null when the owner has not enabled credit ("fiado") for the customer.</param>
public sealed record CustomerDto(
    Guid Id,
    string FullName,
    DocumentType DocumentType,
    string DocumentNumber,
    string? Phone,
    string? Email,
    CreditAccountDto? Credit)
{
    public static CustomerDto From(Customer customer, CreditAccount? account, DateOnly today) =>
        new(customer.Id, customer.FullName, customer.DocumentType, customer.DocumentNumber, customer.Phone, customer.Email,
            account is null ? null : CreditAccountDto.From(account, today));
}
