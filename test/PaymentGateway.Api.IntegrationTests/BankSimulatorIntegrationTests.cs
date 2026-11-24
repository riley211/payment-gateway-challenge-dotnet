using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.IntegrationTests;

public class BankSimulatorIntegrationTests : IClassFixture<BankSimulatorFixture>
{
    private readonly HttpClient _client;

    public BankSimulatorIntegrationTests(BankSimulatorFixture bankSimulator)
    {
        _client = new WebApplicationFactory<PaymentsController>()
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BankSimulator:BaseUrl"] = bankSimulator.BaseUrl
                })))
            .CreateClient();
    }

    [Fact]
    public async Task AuthorizesACardTheBankApproves()
    {
        var created = await _client.PostAsJsonAsync("/api/Payments", PaymentWithCard("2222405343248877"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var payment = await created.Content.ReadFromJsonAsync<PostPaymentResponse>();
        Assert.Equal(PaymentStatus.Authorized, payment!.Status);

        var retrieved = await _client.GetFromJsonAsync<GetPaymentResponse>(created.Headers.Location);
        Assert.Equal(PaymentStatus.Authorized, retrieved!.Status);
        Assert.Equal("8877", retrieved.CardNumberLastFour);
    }

    [Fact]
    public async Task DeclinesACardTheBankRefuses()
    {
        var created = await _client.PostAsJsonAsync("/api/Payments", PaymentWithCard("2222405343248878"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var payment = await created.Content.ReadFromJsonAsync<PostPaymentResponse>();
        Assert.Equal(PaymentStatus.Declined, payment!.Status);
    }

    [Fact]
    public async Task Returns502WhenTheBankIsUnavailable()
    {
        var response = await _client.PostAsJsonAsync("/api/Payments", PaymentWithCard("2222405343248870"));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    private static object PaymentWithCard(string cardNumber) => new
    {
        cardNumber,
        expiryMonth = 12,
        expiryYear = DateTime.UtcNow.Year + 1,
        currency = "GBP",
        amount = 1050,
        cvv = "123"
    };
}