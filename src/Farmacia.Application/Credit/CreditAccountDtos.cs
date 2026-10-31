using Farmacia.Domain.Credit;

namespace Farmacia.Application.Credit;

/// <param name="Available">What the customer can still take on credit.</param>
/// <param name="DaysOwing">Days since the customer started owing without interruption; null when nothing is owed.</param>
public sealed record CreditAccountDto(
    decimal Limit,
    decimal Balance,
    decimal Available,
    bool IsSuspended,
    DateTimeOffset? DebtSince,
    int? DaysOwing)
{
    /// <param name="today">The pharmacy's date (Colombia), not the server's.</param>
    public static CreditAccountDto From(CreditAccount account, DateOnly today) =>
        new(account.Limit, account.Balance, account.Available, account.IsSuspended, account.DebtSince,
            account.DebtSince is { } since ? today.DayNumber - DateOnly.FromDateTime(since.DateTime).DayNumber : null);
}

/// <param name="Change">Positive when the debt went up, negative when it went down.</param>
/// <param name="ReferenceId">The sale, payment or return that caused it.</param>
public sealed record CreditEntryDto(CreditEntryType Type, decimal Change, decimal BalanceAfter, DateTimeOffset OccurredAt, Guid ReferenceId);

/// <summary>What the customer owes and how it got there, newest first: what the cashier tells the customer.</summary>
public sealed record CreditStatementDto(
    Guid CustomerId,
    string CustomerName,
    CreditAccountDto Account,
    IReadOnlyList<CreditEntryDto> Entries);

/// <summary>A line of the owner's list of who owes.</summary>
public sealed record DebtorDto(
    Guid CustomerId,
    string CustomerName,
    string DocumentNumber,
    string? Phone,
    CreditAccountDto Account);
