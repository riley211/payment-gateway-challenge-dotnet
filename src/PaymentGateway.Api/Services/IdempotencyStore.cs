using System.Collections.Concurrent;

using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public class IdempotencyStore
{
    private readonly ConcurrentDictionary<string, PostPaymentResponse> _payments = new();

    public async Task<PostPaymentResponse> ProcessOnceAsync(string idempotencyKey, Func<Task<PostPaymentResponse>> processPayment)
    {
        if (_payments.TryGetValue(idempotencyKey, out var existing))
        {
            return existing;
        }

        var payment = await processPayment();
        _payments[idempotencyKey] = payment;

        return payment;
    }
}
