using System.Collections.Concurrent;

using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public class IdempotencyStore
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public async Task<PostPaymentResponse> ProcessOnceAsync(
        string idempotencyKey,
        string requestFingerprint,
        Func<Task<PostPaymentResponse>> processPayment)
    {
        // GetOrAdd can build more than one Entry under a race, but only the stored one is ever started.
        var entry = _entries.GetOrAdd(
            idempotencyKey,
            _ => new Entry(requestFingerprint, new Lazy<Task<PostPaymentResponse>>(processPayment)));

        if (entry.RequestFingerprint != requestFingerprint)
        {
            throw new IdempotencyKeyReusedException();
        }

        try
        {
            return await entry.Payment.Value;
        }
        catch
        {
            _entries.TryRemove(KeyValuePair.Create(idempotencyKey, entry));
            throw;
        }
    }

    private record Entry(string RequestFingerprint, Lazy<Task<PostPaymentResponse>> Payment);
}