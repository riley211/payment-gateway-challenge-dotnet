using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public class PaymentsRepository : IPaymentsRepository
{
    private readonly Lock _lock = new();

    public List<PostPaymentResponse> Payments = new();

    public Task AddAsync(PostPaymentResponse payment)
    {
        lock (_lock)
        {
            Payments.Add(payment);
        }

        return Task.CompletedTask;
    }

    public Task<PostPaymentResponse?> GetAsync(Guid id)
    {
        lock (_lock)
        {
            return Task.FromResult(Payments.FirstOrDefault(p => p.Id == id));
        }
    }
}