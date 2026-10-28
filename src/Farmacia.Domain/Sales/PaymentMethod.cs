namespace Farmacia.Domain.Sales;

public enum PaymentMethod
{
    /// <summary>The only method that gives change.</summary>
    Cash = 1,

    /// <summary>Debit or credit card, through the card terminal.</summary>
    Card = 2,

    /// <summary>Nequi, Daviplata and similar mobile wallets.</summary>
    DigitalWallet = 3,

    BankTransfer = 4,
}
