using System.Net;
using System.Text;

using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class BankSimulatorClientTests
{
    private static readonly BankPaymentRequest Payment = new("2222405343248877", "04/2030", "GBP", 100, "123");

    [Fact]
    public async Task PostsThePaymentAsSnakeCaseJson()
    {
        var handler = new StubHttpMessageHandler(() => Json(HttpStatusCode.OK, """{"authorized":true,"authorization_code":"abc"}"""));

        await CreateClient(handler).ProcessPaymentAsync(Payment);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal(new Uri("http://bank.test/payments"), handler.Request.RequestUri);
        Assert.Equal(
            """{"card_number":"2222405343248877","expiry_date":"04/2030","currency":"GBP","amount":100,"cvv":"123"}""",
            handler.RequestBody);
    }

    [Theory]
    [InlineData("""{"authorized":true,"authorization_code":"abc"}""", true, "abc")]
    [InlineData("""{"authorized":false,"authorization_code":""}""", false, "")]
    public async Task ReturnsTheBanksDecision(string body, bool authorized, string authorizationCode)
    {
        var handler = new StubHttpMessageHandler(() => Json(HttpStatusCode.OK, body));

        var response = await CreateClient(handler).ProcessPaymentAsync(Payment);

        Assert.Equal(new BankPaymentResponse(authorized, authorizationCode), response);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task ThrowsWhenTheBankDoesNotSucceed(HttpStatusCode statusCode)
    {
        var handler = new StubHttpMessageHandler(() => Json(statusCode, "{}"));

        await Assert.ThrowsAsync<BankUnavailableException>(() => CreateClient(handler).ProcessPaymentAsync(Payment));
    }

    [Fact]
    public async Task ThrowsWhenTheBankCannotBeReached()
    {
        var handler = new StubHttpMessageHandler(() => throw new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<BankUnavailableException>(() => CreateClient(handler).ProcessPaymentAsync(Payment));
    }

    [Fact]
    public async Task ThrowsWhenTheBankTimesOut()
    {
        var handler = new StubHttpMessageHandler(() => throw new TaskCanceledException());

        await Assert.ThrowsAsync<BankUnavailableException>(() => CreateClient(handler).ProcessPaymentAsync(Payment));
    }

    private static BankSimulatorClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://bank.test/") });

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body) =>
        new(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}