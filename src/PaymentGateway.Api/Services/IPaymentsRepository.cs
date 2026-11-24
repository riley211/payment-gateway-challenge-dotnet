using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public interface IPaymentsRepository
{
    Task AddAsync(PostPaymentResponse payment);

    Task<PostPaymentResponse?> GetAsync(Guid id);
}