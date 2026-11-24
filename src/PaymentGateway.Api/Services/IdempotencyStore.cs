using System.Collections.Concurrent;

using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public class IdempotencyStore
{
    private readonly ConcurrentDictionary<string, Lazy<Task<PostPaymentResponse>>> _payments = new();

    public Task<PostPaymentResponse> ProcessOnceAsync(string idempotencyKey, Func<Task<PostPaymentResponse>> processPayment)
    {
        // GetOrAdd can build more than one Lazy under a race, but only the stored one is ever started.
        var payment = _payments.GetOrAdd(idempotencyKey, _ => new Lazy<Task<PostPaymentResponse>>(processPayment));

        return payment.Value;
    }
}
