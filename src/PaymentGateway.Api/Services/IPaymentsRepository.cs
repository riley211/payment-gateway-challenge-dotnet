using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Services;

public interface IPaymentsRepository
{
    Task AddAsync(Payment payment);

    Task<Payment?> GetAsync(Guid id);
}