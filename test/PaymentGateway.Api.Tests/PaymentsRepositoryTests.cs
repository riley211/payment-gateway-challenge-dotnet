using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Models;
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

    private static Payment Payment() => new(
        Id: Guid.NewGuid(),
        Status: PaymentStatus.Authorized,
        CardNumberLastFour: "8877",
        ExpiryMonth: 4,
        ExpiryYear: 2030,
        Currency: "GBP",
        Amount: 1050,
        AuthorizationCode: "0bb07405-6d44-4b50-a14f-7ae0beff13ad",
        CreatedAt: DateTimeOffset.UtcNow);
}