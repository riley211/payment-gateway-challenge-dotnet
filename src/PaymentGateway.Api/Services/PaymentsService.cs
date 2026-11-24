using System.Security.Cryptography;
using System.Text.Json;

using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public class PaymentsService(
    IAcquiringBank bank,
    IPaymentsRepository paymentsRepository,
    IdempotencyStore idempotencyStore,
    PaymentMetrics metrics,
    TimeProvider timeProvider,
    ILogger<PaymentsService> logger)
{
    public Task<Payment> ProcessPaymentAsync(PostPaymentRequest request, string? idempotencyKey)
    {
        return idempotencyKey is null
            ? AuthorizeAsync(request)
            : idempotencyStore.ProcessOnceAsync(idempotencyKey, Fingerprint(request), () => AuthorizeAsync(request));
    }

    private static string Fingerprint(PostPaymentRequest request) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));

    private async Task<Payment> AuthorizeAsync(PostPaymentRequest request)
    {
        BankPaymentResponse bankResponse;
        try
        {
            bankResponse = await bank.ProcessPaymentAsync(BankPaymentRequest.From(request));
        }
        catch (BankUnavailableException exception)
        {
            metrics.BankUnavailable();
            logger.LogWarning(exception, "The acquiring bank was unavailable, so the payment was not processed");
            throw;
        }

        var payment = new Payment(
            Id: Guid.NewGuid(),
            Status: bankResponse.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour: request.CardNumber![^4..],
            ExpiryMonth: request.ExpiryMonth!.Value,
            ExpiryYear: request.ExpiryYear!.Value,
            Currency: request.Currency!,
            Amount: request.Amount!.Value,
            AuthorizationCode: bankResponse.Authorized ? bankResponse.AuthorizationCode : null,
            CreatedAt: timeProvider.GetUtcNow());

        await paymentsRepository.AddAsync(payment);
        metrics.PaymentProcessed(payment.Status);
        logger.LogInformation(
            "Payment {PaymentId} for card ending {CardNumberLastFour} was {PaymentStatus} for {Amount} {Currency}",
            payment.Id, payment.CardNumberLastFour, payment.Status, payment.Amount, payment.Currency);

        return payment;
    }
}