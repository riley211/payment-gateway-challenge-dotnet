using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Models.Bank;

public record BankPaymentRequest(string CardNumber, string ExpiryDate, string Currency, int Amount, string Cvv)
{
    public static BankPaymentRequest From(PostPaymentRequest payment) => new(
        payment.CardNumber!,
        $"{payment.ExpiryMonth:D2}/{payment.ExpiryYear}",
        payment.Currency!,
        payment.Amount!.Value,
        payment.Cvv!);
}