using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Tests;

public class BankPaymentRequestTests
{
    [Theory]
    [InlineData(4, "04/2030")]
    [InlineData(12, "12/2030")]
    public void FormatsTheExpiryAsMonthAndYear(int expiryMonth, string expectedExpiryDate)
    {
        var payment = new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = expiryMonth,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        var bankRequest = BankPaymentRequest.From(payment);

        Assert.Equal(new BankPaymentRequest("2222405343248877", expectedExpiryDate, "GBP", 100, "123"), bankRequest);
    }
}
