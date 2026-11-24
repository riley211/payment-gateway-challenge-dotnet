using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class ProcessPaymentTests
{
    private readonly FakeAcquiringBank _bank = new();
    private readonly PaymentsRepository _repository = new();
    private readonly HttpClient _client;

    public ProcessPaymentTests()
    {
        _client = new WebApplicationFactory<PaymentsController>()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.AddSingleton(_repository);
                services.AddSingleton<IAcquiringBank>(_bank);
            }))
            .CreateClient();
    }

    [Fact]
    public async Task AuthorizesAPaymentTheBankApproves()
    {
        var response = await _client.PostAsJsonAsync("/api/Payments", ValidPayment());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();
        Assert.Equal(PaymentStatus.Authorized, payment!.Status);
        Assert.Equal("8877", payment.CardNumberLastFour);
        Assert.Equal(4, payment.ExpiryMonth);
        Assert.Equal(2030, payment.ExpiryYear);
        Assert.Equal("GBP", payment.Currency);
        Assert.Equal(1050, payment.Amount);
        Assert.Equal(new BankPaymentRequest("2222405343248877", "04/2030", "GBP", 1050, "123"), Assert.Single(_bank.Requests));
    }

    [Fact]
    public async Task DeclinesAPaymentTheBankRefuses()
    {
        _bank.Response = new BankPaymentResponse(false, "");

        var response = await _client.PostAsJsonAsync("/api/Payments", ValidPayment());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();
        Assert.Equal(PaymentStatus.Declined, payment!.Status);
    }

    [Fact]
    public async Task StoresThePaymentSoItCanBeRetrieved()
    {
        var created = await _client.PostAsJsonAsync("/api/Payments", ValidPayment());
        var payment = await created.Content.ReadFromJsonAsync<PostPaymentResponse>();

        Assert.EndsWith($"/api/Payments/{payment!.Id}", created.Headers.Location!.ToString());
        var retrieved = await _client.GetFromJsonAsync<GetPaymentResponse>(created.Headers.Location);
        Assert.Equal(payment.Id, retrieved!.Id);
        Assert.Equal(payment.Status, retrieved.Status);
        Assert.Equal("8877", retrieved.CardNumberLastFour);
    }

    [Fact]
    public async Task NeverReturnsTheFullCardNumberOrCvv()
    {
        var created = await _client.PostAsJsonAsync("/api/Payments", ValidPayment());
        var createdJson = await created.Content.ReadAsStringAsync();
        var retrievedJson = await _client.GetStringAsync(created.Headers.Location);

        foreach (var json in new[] { createdJson, retrievedJson })
        {
            Assert.DoesNotContain("2222405343248877", json);
            Assert.DoesNotContain("cvv", json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Returns502AndStoresNothingWhenTheBankIsUnavailable()
    {
        _bank.IsUnavailable = true;

        var response = await _client.PostAsJsonAsync("/api/Payments", ValidPayment());

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Empty(_repository.Payments);
    }

    private static object ValidPayment() => new
    {
        cardNumber = "2222405343248877",
        expiryMonth = 4,
        expiryYear = 2030,
        currency = "GBP",
        amount = 1050,
        cvv = "123"
    };
}
