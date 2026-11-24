using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public class PaymentsRepository
{
    private readonly Lock _lock = new();

    public List<PostPaymentResponse> Payments = new();

    public void Add(PostPaymentResponse payment)
    {
        lock (_lock)
        {
            Payments.Add(payment);
        }
    }

    public PostPaymentResponse? Get(Guid id)
    {
        lock (_lock)
        {
            return Payments.FirstOrDefault(p => p.Id == id);
        }
    }
}