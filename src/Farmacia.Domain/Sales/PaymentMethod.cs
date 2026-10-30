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

    /// <summary>"Fiado": charged to the customer's credit account, paid later with "abonos".</summary>
    Credit = 5,
}
