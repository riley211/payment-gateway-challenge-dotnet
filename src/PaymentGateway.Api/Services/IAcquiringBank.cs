using PaymentGateway.Api.Models.Bank;

namespace PaymentGateway.Api.Services;

public interface IAcquiringBank
{
    Task<BankPaymentResponse> ProcessPaymentAsync(BankPaymentRequest request);
}