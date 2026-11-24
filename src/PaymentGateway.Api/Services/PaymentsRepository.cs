using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Services;

public class PaymentsRepository : IPaymentsRepository
{
    private readonly Lock _lock = new();

    public List<Payment> Payments = new();

    public Task AddAsync(Payment payment)
    {
        lock (_lock)
        {
            Payments.Add(payment);
        }

        return Task.CompletedTask;
    }

    public Task<Payment?> GetAsync(Guid id)
    {
        lock (_lock)
        {
            return Task.FromResult(Payments.FirstOrDefault(p => p.Id == id));
        }
    }
}