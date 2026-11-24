using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public class PaymentsService(IAcquiringBank bank, PaymentsRepository paymentsRepository, IdempotencyStore idempotencyStore)
{
    public Task<PostPaymentResponse> ProcessPaymentAsync(PostPaymentRequest request, string? idempotencyKey)
    {
        return idempotencyKey is null
            ? AuthorizeAsync(request)
            : idempotencyStore.ProcessOnceAsync(idempotencyKey, () => AuthorizeAsync(request));
    }

    private async Task<PostPaymentResponse> AuthorizeAsync(PostPaymentRequest request)
    {
        var bankResponse = await bank.ProcessPaymentAsync(BankPaymentRequest.From(request));

        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = bankResponse.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour = request.CardNumber![^4..],
            ExpiryMonth = request.ExpiryMonth!.Value,
            ExpiryYear = request.ExpiryYear!.Value,
            Currency = request.Currency!,
            Amount = request.Amount!.Value
        };

        paymentsRepository.Add(payment);

        return payment;
    }
}
