using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class PaymentsRepositoryTests
{
    [Fact]
    public async Task KeepsEveryPaymentAddedConcurrently()
    {
        var repository = new PaymentsRepository();
        var payments = Enumerable.Range(0, 10_000).Select(_ => Payment()).ToList();

        await Parallel.ForEachAsync(payments, async (payment, _) => await repository.AddAsync(payment));

        foreach (var payment in payments)
        {
            Assert.Same(payment, await repository.GetAsync(payment.Id));
        }
    }

    private static PostPaymentResponse Payment() => new()
    {
        Id = Guid.NewGuid(),
        Status = PaymentStatus.Authorized,
        CardNumberLastFour = "8877",
        ExpiryMonth = 4,
        ExpiryYear = 2030,
        Currency = "GBP",
        Amount = 1050
    };
}