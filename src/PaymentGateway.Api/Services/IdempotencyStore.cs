using System.Collections.Concurrent;

using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Services;

public class IdempotencyStore
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public async Task<Payment> ProcessOnceAsync(
        string idempotencyKey,
        string requestFingerprint,
        Func<Task<Payment>> processPayment)
    {
        // GetOrAdd can build more than one Entry under a race, but only the stored one is ever started.
        var entry = _entries.GetOrAdd(
            idempotencyKey,
            _ => new Entry(requestFingerprint, new Lazy<Task<Payment>>(processPayment)));

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

    private sealed record Entry(string RequestFingerprint, Lazy<Task<Payment>> Payment);
}